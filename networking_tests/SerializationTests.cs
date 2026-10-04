namespace Keel.Networking.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void PackedUInt32()
        {
            var writer = new NetWriter();

            // Should take 1 byte.
            writer.WritePackedUInt32(128);
            Assert.Equal(1, writer.Position);

            // Should take 2 bytes.
            writer.WritePackedUInt32(600);
            Assert.Equal(1 + 2, writer.Position);

            // Should take 5 bytes.
            writer.WritePackedUInt32(uint.MaxValue);
            Assert.Equal(1 + 2 + 5, writer.Position);
        }

        [Fact]
        public void ReadUInt32()
        {
            var writer = new NetWriter();
            var dataToTest = new uint[]
            {
                128, byte.MaxValue, 600, 512, 8000, 16000, ushort.MaxValue, ushort.MaxValue * 3 + 10, uint.MaxValue
            };

            foreach (var data in dataToTest)
                writer.WritePackedUInt32(data);

            var reader = new NetReader(writer);
            foreach (var data in dataToTest)
                Assert.Equal(data, reader.ReadPackedUInt32());
        }

        [Fact]
        public void ReadRandomUInt32()
        {
            var rnd = new Random();
            var writer = new NetWriter();
            var dataToTest = new uint[2048];

            for (var i = 0; i < dataToTest.Length; i++)
                dataToTest[i] = (uint)rnd.NextInt64();

            foreach (var data in dataToTest)
                writer.WritePackedUInt32(data);

            var reader = new NetReader(writer);
            foreach (var data in dataToTest)
                Assert.Equal(data, reader.ReadPackedUInt32());
        }

        [Fact]
        public void ReadRandomString()
        {
            var rnd = new Random();
            var writer = new NetWriter();
            var dataToTest = new string[512];

            for (var i = 0; i < dataToTest.Length; i++)
                dataToTest[i] = GetRandomString(length: rnd.Next(5, 100), rnd);

            foreach (var data in dataToTest)
                writer.WriteString(data);

            var reader = new NetReader(writer);
            foreach (var data in dataToTest)
                Assert.Equal(data, reader.ReadString());

            return;

            static string GetRandomString(int length, Random random)
            {
                var stringChars = new char[length];
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                for (var i = 0; i < stringChars.Length; i++)
                    stringChars[i] = chars[random.Next(chars.Length)];

                return new string(stringChars);
            }
        }

        [Fact]
        public void UnmanagedSerialization()
        {
            const string headerString = "Hello World!";
            var float3 = new Float3Blittable
            {
                x = 0.12321f,
                y = -12.2f,
                z = 1000
            };

            var nonBlittable = new NonBlittableData
            {
                Float3Blittable0 = new Float3Blittable()
                {
                    x = 11,
                    y = -600,
                    z = 1000
                },
                Char0 = 'j',
                Bool0 = true,
                Char1 = 'b',
                Float3Blittable1 = new Float3Blittable()
                {
                    x = 12,
                    y = 601,
                    z = -200
                },
                Bool1 = false,
                Half3Blittable0 = new Half3Blittable()
                {
                    x = 13,
                    y = -2000,
                    z = 3
                },
                Bool2 = true
            };

            var half3 = new Half3Blittable
            {
                x = 12,
                y = -600,
                z = short.MaxValue
            };

            var writer = new NetWriter();
            writer.WriteUnmanaged(float3);
            writer.WriteString(headerString);
            writer.WriteUnmanaged(nonBlittable);
            writer.WriteString(headerString);
            writer.WriteUnmanaged(half3);

            var reader = new NetReader(writer);
            var float3Read = reader.ReadUnmanaged<Float3Blittable>();
            var headerStringRead0 = reader.ReadString();
            var notBlittableRead = reader.ReadUnmanaged<NonBlittableData>();
            var headerStringRead1 = reader.ReadString();
            var half3Read = reader.ReadUnmanaged<Half3Blittable>();

            Assert.Equal(float3, float3Read);
            Assert.Equal(headerString, headerStringRead0);
            Assert.Equal(nonBlittable, notBlittableRead);
            Assert.Equal(headerString, headerStringRead1);
            Assert.Equal(half3, half3Read);
        }

        [Fact]
        public void WriteRead_Boolean_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteBoolean(true);
            writer.WriteBoolean(false);
            writer.WriteBoolean(true);

            var reader = new NetReader(writer);
            Assert.True(reader.ReadBoolean());
            Assert.False(reader.ReadBoolean());
            Assert.True(reader.ReadBoolean());
        }

        [Fact]
        public void WriteRead_Byte_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteByte(0);
            writer.WriteByte(127);
            writer.WriteByte(255);

            var reader = new NetReader(writer);
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(127, reader.ReadByte());
            Assert.Equal(255, reader.ReadByte());
        }

        [Fact]
        public void WriteRead_Int16_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteInt16(short.MinValue);
            writer.WriteInt16(0);
            writer.WriteInt16(short.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(short.MinValue, reader.ReadInt16());
            Assert.Equal(0, reader.ReadInt16());
            Assert.Equal(short.MaxValue, reader.ReadInt16());
        }

        [Fact]
        public void WriteRead_Int32_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteInt32(int.MinValue);
            writer.WriteInt32(-1);
            writer.WriteInt32(0);
            writer.WriteInt32(1);
            writer.WriteInt32(int.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(int.MinValue, reader.ReadInt32());
            Assert.Equal(-1, reader.ReadInt32());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal(int.MaxValue, reader.ReadInt32());
        }

        [Fact]
        public void WriteRead_UInt16_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteUInt16(0);
            writer.WriteUInt16(32768);
            writer.WriteUInt16(ushort.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(0, reader.ReadUInt16());
            Assert.Equal(32768, reader.ReadUInt16());
            Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
        }

        [Fact]
        public void WriteRead_UInt32_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteUInt32(0);
            writer.WriteUInt32(uint.MaxValue / 2);
            writer.WriteUInt32(uint.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(0u, reader.ReadUInt32());
            Assert.Equal(uint.MaxValue / 2, reader.ReadUInt32());
            Assert.Equal(uint.MaxValue, reader.ReadUInt32());
        }

        [Fact]
        public void WriteRead_Int64_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteInt64(long.MinValue);
            writer.WriteInt64(0);
            writer.WriteInt64(long.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(long.MinValue, reader.ReadInt64());
            Assert.Equal(0, reader.ReadInt64());
            Assert.Equal(long.MaxValue, reader.ReadInt64());
        }

        [Fact]
        public void WriteRead_UInt64_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteUInt64(0);
            writer.WriteUInt64(ulong.MaxValue / 2);
            writer.WriteUInt64(ulong.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(0ul, reader.ReadUInt64());
            Assert.Equal(ulong.MaxValue / 2, reader.ReadUInt64());
            Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
        }

        [Fact]
        public void WriteRead_Single_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteSingle(-123.456f);
            writer.WriteSingle(0.0f);
            writer.WriteSingle(float.MaxValue);
            writer.WriteSingle(float.MinValue);

            var reader = new NetReader(writer);
            Assert.Equal(-123.456f, reader.ReadSingle());
            Assert.Equal(0.0f, reader.ReadSingle());
            Assert.Equal(float.MaxValue, reader.ReadSingle());
            Assert.Equal(float.MinValue, reader.ReadSingle());
        }

        [Fact]
        public void WriteRead_Double_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteDouble(-123.456789);
            writer.WriteDouble(0.0);
            writer.WriteDouble(double.MaxValue);
            writer.WriteDouble(double.MinValue);

            var reader = new NetReader(writer);
            Assert.Equal(-123.456789, reader.ReadDouble());
            Assert.Equal(0.0, reader.ReadDouble());
            Assert.Equal(double.MaxValue, reader.ReadDouble());
            Assert.Equal(double.MinValue, reader.ReadDouble());
        }

        [Fact]
        public void WriteRead_EmptyString_RoundTrips()
        {
            var writer = new NetWriter();
            writer.WriteString("");

            var reader = new NetReader(writer);
            Assert.Equal("", reader.ReadString());
        }

        [Fact]
        public void WriteRead_StringWithSpecialCharacters_RoundTrips()
        {
            var writer = new NetWriter();
            var testStrings = new[] { "Hello 世界", "Привет мир", "مرحبا بالعالم", "🎉🎊🎈" };

            foreach (var str in testStrings)
                writer.WriteString(str);

            var reader = new NetReader(writer);
            foreach (var str in testStrings)
                Assert.Equal(str, reader.ReadString());
        }

        [Fact]
        public void NetWriter_SeekZero_ResetsPosition()
        {
            var writer = new NetWriter();
            writer.WriteInt32(123);
            writer.WriteString("test");
            Assert.True(writer.Position > 0);

            writer.SeekZero();
            Assert.Equal(0, writer.Position);

            // Can reuse after SeekZero
            writer.WriteInt32(456);
            var reader = new NetReader(writer);
            Assert.Equal(456, reader.ReadInt32());
        }

        [Fact]
        public void NetReader_Replace_AllowsBufferReuse()
        {
            var writer1 = new NetWriter();
            writer1.WriteInt32(100);

            var writer2 = new NetWriter();
            writer2.WriteInt32(200);

            var reader = new NetReader(writer1);
            Assert.Equal(100, reader.ReadInt32());

            reader.Replace(writer2.ToArray());
            Assert.Equal(200, reader.ReadInt32());
        }

        [Fact]
        public void MixedSerialization_MaintainsOrder()
        {
            var writer = new NetWriter();
            writer.WriteByte(42);
            writer.WriteString("test");
            writer.WriteInt32(12345);
            writer.WriteBoolean(true);
            writer.WriteSingle(3.14f);
            writer.WritePackedUInt32(999);

            var reader = new NetReader(writer);
            Assert.Equal(42, reader.ReadByte());
            Assert.Equal("test", reader.ReadString());
            Assert.Equal(12345, reader.ReadInt32());
            Assert.True(reader.ReadBoolean());
            Assert.Equal(3.14f, reader.ReadSingle());
            Assert.Equal(999u, reader.ReadPackedUInt32());
        }

        [Fact]
        public void PackedUInt32_SmallValues_UseMinimalBytes()
        {
            var writer = new NetWriter();

            // Values <= 240 should take 1 byte
            writer.WritePackedUInt32(0);
            Assert.Equal(1, writer.Position);

            writer.WritePackedUInt32(240);
            Assert.Equal(2, writer.Position);

            // Values 241-2287 should take 2 bytes
            writer.SeekZero();
            writer.WritePackedUInt32(241);
            Assert.Equal(2, writer.Position);

            writer.WritePackedUInt32(2287);
            Assert.Equal(4, writer.Position);
        }

        [Fact]
        public void ByteArray_WriteThenRead_RoundTrips()
        {
            var writer = new NetWriter();
            var testData = new byte[] { 1, 2, 3, 4, 5, 10, 20, 30, 255 };

            foreach (var b in testData)
                writer.WriteByte(b);

            var reader = new NetReader(writer);
            foreach (var expected in testData)
                Assert.Equal(expected, reader.ReadByte());
        }

        [Fact]
        public void LargeDataSet_SerializesCorrectly()
        {
            var writer = new NetWriter();
            var testInts = new int[1000];
            var rnd = new Random(42);

            for (var i = 0; i < testInts.Length; i++)
            {
                testInts[i] = rnd.Next(int.MinValue, int.MaxValue);
                writer.WriteInt32(testInts[i]);
            }

            var reader = new NetReader(writer);
            for (var i = 0; i < testInts.Length; i++)
                Assert.Equal(testInts[i], reader.ReadInt32());
        }

        [Fact]
        public void ZeroValues_SerializeCorrectly()
        {
            var writer = new NetWriter();
            writer.WriteByte(0);
            writer.WriteInt16(0);
            writer.WriteInt32(0);
            writer.WriteInt64(0);
            writer.WriteUInt16(0);
            writer.WriteUInt32(0);
            writer.WriteUInt64(0);
            writer.WriteSingle(0.0f);
            writer.WriteDouble(0.0);
            writer.WritePackedUInt32(0);

            var reader = new NetReader(writer);
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(0, reader.ReadInt16());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(0L, reader.ReadInt64());
            Assert.Equal(0, reader.ReadUInt16());
            Assert.Equal(0u, reader.ReadUInt32());
            Assert.Equal(0ul, reader.ReadUInt64());
            Assert.Equal(0.0f, reader.ReadSingle());
            Assert.Equal(0.0, reader.ReadDouble());
            Assert.Equal(0u, reader.ReadPackedUInt32());
        }

        [Fact]
        public void MaxValues_SerializeCorrectly()
        {
            var writer = new NetWriter();
            writer.WriteByte(byte.MaxValue);
            writer.WriteInt16(short.MaxValue);
            writer.WriteInt32(int.MaxValue);
            writer.WriteInt64(long.MaxValue);
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteUInt32(uint.MaxValue);
            writer.WriteUInt64(ulong.MaxValue);
            writer.WriteSingle(float.MaxValue);
            writer.WriteDouble(double.MaxValue);
            writer.WritePackedUInt32(uint.MaxValue);

            var reader = new NetReader(writer);
            Assert.Equal(byte.MaxValue, reader.ReadByte());
            Assert.Equal(short.MaxValue, reader.ReadInt16());
            Assert.Equal(int.MaxValue, reader.ReadInt32());
            Assert.Equal(long.MaxValue, reader.ReadInt64());
            Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
            Assert.Equal(uint.MaxValue, reader.ReadUInt32());
            Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
            Assert.Equal(float.MaxValue, reader.ReadSingle());
            Assert.Equal(double.MaxValue, reader.ReadDouble());
            Assert.Equal(uint.MaxValue, reader.ReadPackedUInt32());
        }

        [Fact]
        public void MinValues_SerializeCorrectly()
        {
            var writer = new NetWriter();
            writer.WriteByte(byte.MinValue);
            writer.WriteInt16(short.MinValue);
            writer.WriteInt32(int.MinValue);
            writer.WriteInt64(long.MinValue);
            writer.WriteUInt16(ushort.MinValue);
            writer.WriteUInt32(uint.MinValue);
            writer.WriteUInt64(ulong.MinValue);
            writer.WriteSingle(float.MinValue);
            writer.WriteDouble(double.MinValue);

            var reader = new NetReader(writer);
            Assert.Equal(byte.MinValue, reader.ReadByte());
            Assert.Equal(short.MinValue, reader.ReadInt16());
            Assert.Equal(int.MinValue, reader.ReadInt32());
            Assert.Equal(long.MinValue, reader.ReadInt64());
            Assert.Equal(ushort.MinValue, reader.ReadUInt16());
            Assert.Equal(uint.MinValue, reader.ReadUInt32());
            Assert.Equal(ulong.MinValue, reader.ReadUInt64());
            Assert.Equal(float.MinValue, reader.ReadSingle());
            Assert.Equal(double.MinValue, reader.ReadDouble());
        }

        private struct Float3Blittable : IEquatable<Float3Blittable>
        {
            public float x, y, z;

            public bool Equals(Float3Blittable other) =>
                x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);

            public override bool Equals(object obj) =>
                obj is Float3Blittable other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(x, y, z);
        }

        private struct Half3Blittable : IEquatable<Half3Blittable>
        {
            public short x, y, z;

            public bool Equals(Half3Blittable other) =>
                x == other.x && y == other.y && z == other.z;

            public override bool Equals(object obj) =>
                obj is Half3Blittable other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(x, y, z);
        }

        private struct NonBlittableData : IEquatable<NonBlittableData>
        {
            public Float3Blittable Float3Blittable0;
            public char Char0;
            public bool Bool0;
            public char Char1;
            public Float3Blittable Float3Blittable1;
            public bool Bool1;
            public Half3Blittable Half3Blittable0;
            public bool Bool2;

            public bool Equals(NonBlittableData other) =>
                Float3Blittable0.Equals(other.Float3Blittable0) &&
                Char0 == other.Char0 &&
                Bool0 == other.Bool0 &&
                Char1 == other.Char1 &&
                Float3Blittable1.Equals(other.Float3Blittable1) &&
                Bool1 == other.Bool1 &&
                Half3Blittable0.Equals(other.Half3Blittable0) &&
                Bool2 == other.Bool2;

            public override bool Equals(object obj) =>
                obj is NonBlittableData other && Equals(other);
            public override int GetHashCode() =>
                HashCode.Combine(Float3Blittable0, Char0, Bool0, Char1, Float3Blittable1, Bool1, Half3Blittable0, Bool2);
        }
    }
}