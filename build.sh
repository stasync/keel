# Usage: sh build.sh [build|test|package]
# With no argument, runs all three stages in order. CI runs them one by one,
# so each stage shows up as its own step.

# Stop at the first failing step, so a failed build or test fails the whole script (and CI).
set -e

reldir="$(dirname "$0")"
cd "$reldir"
BUILD_CONFIGURATION="release"
DOTNET_ARTIFACTS_DIR="artifacts"

# Project paths
UTILS_PROJ="utils/utils.csproj"
NETWORKING_PROJ="networking/networking.csproj"
DI_PROJ="dependency_injection/dependency_injection.csproj"
DI_TESTS_PROJ="dependency_injection_tests/dependency_injection_tests.csproj"
NETWORKING_TESTS_PROJ="networking_tests/networking_tests.csproj"

OUT_DIR="$DOTNET_ARTIFACTS_DIR/delivery"

stage_build() {
    rm -rf $DOTNET_ARTIFACTS_DIR

    #https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build
    dotnet clean $UTILS_PROJ
    dotnet clean $NETWORKING_PROJ
    dotnet clean $DI_PROJ
    dotnet clean $DI_TESTS_PROJ
    dotnet clean $NETWORKING_TESTS_PROJ

    dotnet build $UTILS_PROJ --no-incremental --configuration $BUILD_CONFIGURATION --force --artifacts-path $DOTNET_ARTIFACTS_DIR
    dotnet build $NETWORKING_PROJ --no-incremental --configuration $BUILD_CONFIGURATION --force --artifacts-path $DOTNET_ARTIFACTS_DIR
    dotnet build $DI_PROJ --no-incremental --configuration $BUILD_CONFIGURATION --force --artifacts-path $DOTNET_ARTIFACTS_DIR
}

stage_test() {
    dotnet test $DI_TESTS_PROJ
    dotnet test $NETWORKING_TESTS_PROJ
}

stage_package() {
    if [ ! -d "$DOTNET_ARTIFACTS_DIR/bin" ]; then
        echo "Nothing to package: '$DOTNET_ARTIFACTS_DIR/bin' is missing. Run 'sh build.sh build' first." >&2
        exit 1
    fi

    # Group output DLLs by target framework (discovered dynamically)
    # No need to delete this directory as its a part of the artifacts directory which is cleaned by the build stage
    for framework_dir in "$DOTNET_ARTIFACTS_DIR"/bin/*/*/; do
        framework=$(basename "$framework_dir")
        mkdir -p "$OUT_DIR/$framework"
        for dll in "$framework_dir"*.dll; do
            if [ -f "$dll" ]; then cp "$dll" "$OUT_DIR/$framework/"; fi
        done
        cp README.md LICENSE "$OUT_DIR/$framework/"
    done

    echo "Output:"
    for dir in "$OUT_DIR"/*/; do
        echo "  $dir: $(ls "$dir"*.dll 2>/dev/null | xargs -n1 basename | tr '\n' ' ')"
    done
}

case "${1:-all}" in
    build)   stage_build ;;
    test)    stage_test ;;
    package) stage_package ;;
    all)
        stage_build
        stage_test
        stage_package
        ;;
    *)
        echo "Unknown stage '$1'. Use: build, test, package, or no argument to run all of them." >&2
        exit 1
        ;;
esac

printf "\033[1;32mDone: %s\033[0m\n" "${1:-all}"
