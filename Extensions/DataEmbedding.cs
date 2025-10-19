public readonly struct EmbeddedInteger
{
    public EmbeddedInteger(short a, short b) => _embeddedValue = ((a & 0xFFFF) << 16) | (b & 0xFFFF);
    public EmbeddedInteger(int embeddedValue) => _embeddedValue = embeddedValue;

    // 0xFFFF = full 16 bits (00000000 00000000 11111111 11111111) < 32 bits full, but 16 bits are 1s.
    // Used for removing the sign extension (or any 1s in the left side) bits with the AND operator.
    // Then, the 'a' is shifted 16 bits, to be stored on the "left hand". While 'b' is AND'd in the right hand with its value.
    // I think I love binary
    readonly int _embeddedValue; // Goes 32 bits to store one value, and leave b as second value
    public readonly short A => (short)(_embeddedValue >> 16); // Retrieve a by shifting back and casting as short.
    public readonly short B => (short)(_embeddedValue & 0xFFFF); // Just AND to get what was already in the right-hand side.
    public override int GetHashCode() =>
        _embeddedValue.GetHashCode();
    public override bool Equals(object obj) =>
        obj is EmbeddedInteger intg && intg._embeddedValue == _embeddedValue;

    public static bool operator ==(EmbeddedInteger left, EmbeddedInteger right) => left._embeddedValue == right._embeddedValue;
    public static bool operator !=(EmbeddedInteger left, EmbeddedInteger right) => left._embeddedValue != right._embeddedValue;

    public static bool operator ==(EmbeddedInteger left, int right) => left._embeddedValue == right;
    public static bool operator !=(EmbeddedInteger left, int right) => left._embeddedValue != right;

    public static implicit operator int(EmbeddedInteger intg) => intg._embeddedValue;
    public static implicit operator EmbeddedInteger(int num) => new(num);
}

public static class IntegerManipulator
{
    // Empty for now
}