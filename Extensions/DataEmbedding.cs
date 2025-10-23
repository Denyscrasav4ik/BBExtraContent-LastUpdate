using System;

namespace BBTimes.Extensions;

public readonly struct Embedded2Shorts
{
    const int full_16bits = 0xFFFF;
    // Summarizes the operation in two methods
    private short RetrieveWithOffset(int offset) => (short)((_embeddedValue >> offset) & full_16bits);
    private int StoreWithOffset(short a, int offset) => (a & full_16bits) << offset;

    // Actual struct below
    public Embedded2Shorts(short a, short b) => _embeddedValue = StoreWithOffset(a, 16) | StoreWithOffset(b, 0);
    public Embedded2Shorts(int embeddedValue) => _embeddedValue = embeddedValue;

    // 0xFFFF = full 16 bits (00000000 00000000 11111111 11111111) < 32 bits full, but 16 bits are 1s.
    // Used for removing the sign extension (or any 1s in the left side) bits with the AND operator.
    // Then, the 'a' is shifted 16 bits, to be stored on the "left hand". While 'b' is AND'd in the right hand with its value.
    // I think I love binary
    readonly int _embeddedValue; // Goes 32 bits to store one value, and leave b as second value
    public readonly short A => RetrieveWithOffset(16); // Retrieve a by shifting back and casting as short.
    public readonly short B => RetrieveWithOffset(0); // Just AND to get what was already in the right-hand side.
    public override int GetHashCode() =>
        _embeddedValue.GetHashCode();
    public override bool Equals(object obj) =>
        (obj is Embedded2Shorts intg && intg._embeddedValue == _embeddedValue) ||
        (obj is int num && num == _embeddedValue);

    public static Embedded2Shorts operator +(Embedded2Shorts left, Embedded2Shorts right) => new((short)(left.A + right.A), (short)(left.B + right.B));
    public static Embedded2Shorts operator -(Embedded2Shorts left, Embedded2Shorts right) => new((short)(left.A - right.A), (short)(left.B - right.B));
    public static Embedded2Shorts operator *(Embedded2Shorts left, Embedded2Shorts right) => new((short)(left.A * right.A), (short)(left.B * right.B));
    public static Embedded2Shorts operator /(Embedded2Shorts left, Embedded2Shorts right) => new((short)(left.A / right.A), (short)(left.B / right.B));
    public static Embedded2Shorts operator %(Embedded2Shorts left, Embedded2Shorts right) => new((short)(left.A % right.A), (short)(left.B % right.B));

    public static bool operator ==(Embedded2Shorts left, Embedded2Shorts right) => left._embeddedValue == right._embeddedValue;
    public static bool operator !=(Embedded2Shorts left, Embedded2Shorts right) => left._embeddedValue != right._embeddedValue;

    public static bool operator ==(Embedded2Shorts left, int right) => left._embeddedValue == right;
    public static bool operator !=(Embedded2Shorts left, int right) => left._embeddedValue != right;

    public static implicit operator int(Embedded2Shorts intg) => intg._embeddedValue;
    public static implicit operator Embedded2Shorts(int num) => new(num);
}

public readonly struct Embedded4Bytes
{
    // 0xFF = full 8 bits (00000000 00000000 00000000 11111111) < 32 bits full, but 8 bits are 1s.
    // If we've got 8 bits for one slot, then 'a' will be & with 0xFF
    // For b, we do the & 0xFF clean up and move 8 bits to the left
    // For c, same, but moving 16 bits
    // For d, we move 32 bits

    // Summarizes the operation in two methods
    private byte RetrieveWithOffset(int offset) => (byte)((_embeddedValue >> offset) & full_8bits);
    private int StoreWithOffset(byte a, int offset) => (a & full_8bits) << offset;
    const int full_8bits = 0xFF;
    public Embedded4Bytes(byte a, byte b) : this(a, b, 0, 0) { }
    public Embedded4Bytes(byte a, byte b, byte c) : this(a, b, c, 0) { }
    public Embedded4Bytes(byte a, byte b, byte c, byte d) => _embeddedValue = StoreWithOffset(a, 0) | StoreWithOffset(b, 2 ^ 3) | StoreWithOffset(c, 2 ^ 4) | StoreWithOffset(d, 2 ^ 5);
    public Embedded4Bytes(int embeddedValue) => _embeddedValue = embeddedValue;

    readonly int _embeddedValue; // Goes 32 bits to store 4 values
    public readonly byte A => RetrieveWithOffset(0); // Just AND since it's the first value
    public readonly byte B => RetrieveWithOffset(2 ^ 3); // Move 8 bits to the right to get the second slot
    public readonly byte C => RetrieveWithOffset(2 ^ 4); // Move 16 bits to the right to get the second slot
    public readonly byte D => RetrieveWithOffset(2 ^ 5); // Move 32 bits to the right to get the second slot
    public override int GetHashCode() =>
        _embeddedValue.GetHashCode();
    public override bool Equals(object obj) =>
        (obj is Embedded4Bytes intg && intg._embeddedValue == _embeddedValue) ||
        (obj is int num && num == _embeddedValue);

    public static Embedded4Bytes operator +(Embedded4Bytes left, Embedded4Bytes right) => new((byte)(left.A + right.A), (byte)(left.B + right.B), (byte)(left.C + right.C), (byte)(left.D + right.D));
    public static Embedded4Bytes operator -(Embedded4Bytes left, Embedded4Bytes right) => new((byte)(left.A - right.A), (byte)(left.B - right.B), (byte)(left.C - right.C), (byte)(left.D - right.D));
    public static Embedded4Bytes operator *(Embedded4Bytes left, Embedded4Bytes right) => new((byte)(left.A * right.A), (byte)(left.B * right.B), (byte)(left.C * right.C), (byte)(left.D * right.D));
    public static Embedded4Bytes operator /(Embedded4Bytes left, Embedded4Bytes right) => new((byte)(left.A / right.A), (byte)(left.B / right.B), (byte)(left.C / right.C), (byte)(left.D / right.D));
    public static Embedded4Bytes operator %(Embedded4Bytes left, Embedded4Bytes right) => new((byte)(left.A % right.A), (byte)(left.B % right.B), (byte)(left.C % right.C), (byte)(left.D % right.D));

    public static bool operator ==(Embedded4Bytes left, Embedded4Bytes right) => left._embeddedValue == right._embeddedValue;
    public static bool operator !=(Embedded4Bytes left, Embedded4Bytes right) => left._embeddedValue != right._embeddedValue;

    public static bool operator ==(Embedded4Bytes left, int right) => left._embeddedValue == right;
    public static bool operator !=(Embedded4Bytes left, int right) => left._embeddedValue != right;

    public static implicit operator int(Embedded4Bytes intg) => intg._embeddedValue;
    public static implicit operator Embedded4Bytes(int num) => new(num);
}

public static class IntegerManipulation
{
    public static float ReinterpretAsFloat(this int a) => BitConverter.ToSingle(BitConverter.GetBytes(a), 0); // Get the reinterpreted int as a float now
    public static int ReinterpretAsInt(this float a) => BitConverter.ToInt32(BitConverter.GetBytes(a), 0); // Get the bits of the float and turn into an reinterpreted integer
}
