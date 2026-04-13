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

dotnet test $DI_TESTS_PROJ
dotnet test $NETWORKING_TESTS_PROJ

# Group output DLLs by target framework (discovered dynamically)
OUT_DIR="$DOTNET_ARTIFACTS_DIR/delivery"
rm -rf "$OUT_DIR"

for framework_dir in "$DOTNET_ARTIFACTS_DIR"/bin/*/*/; do
    framework=$(basename "$framework_dir")
    mkdir -p "$OUT_DIR/$framework"
    for dll in "$framework_dir"*.dll; do
        [ -f "$dll" ] && cp "$dll" "$OUT_DIR/$framework/"
    done
    cp README.md "$OUT_DIR/$framework/"
done

echo "Output:"
for dir in "$OUT_DIR"/*/; do
    echo "  $dir: $(ls "$dir"*.dll 2>/dev/null | xargs -n1 basename | tr '\n' ' ')"
done
echo -e "\033[1;32mBuild complete!\033[0m"