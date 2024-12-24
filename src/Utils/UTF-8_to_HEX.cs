﻿using System.Collections.Immutable;

namespace WebHost.Utils;

public static class Utf8ToHexMap
{
    public static readonly ImmutableDictionary<string, byte> Map = ImmutableDictionary.CreateRange([
        new KeyValuePair<string, byte>("\\0", 0x00),
        new KeyValuePair<string, byte>("\\x01", 0x01),
        new KeyValuePair<string, byte>("\\x02", 0x02),
        new KeyValuePair<string, byte>("\\x03", 0x03),
        new KeyValuePair<string, byte>("\\x04", 0x04),
        new KeyValuePair<string, byte>("\\x05", 0x05),
        new KeyValuePair<string, byte>("\\x06", 0x06),
        new KeyValuePair<string, byte>("\\a", 0x07),
        new KeyValuePair<string, byte>("\\b", 0x08),
        new KeyValuePair<string, byte>("\\t", 0x09),
        new KeyValuePair<string, byte>("\\n", 0x0A),
        new KeyValuePair<string, byte>("\\v", 0x0B),
        new KeyValuePair<string, byte>("\\f", 0x0C),
        new KeyValuePair<string, byte>("\\r", 0x0D),
        new KeyValuePair<string, byte>("\\x0E", 0x0E),
        new KeyValuePair<string, byte>("\\x0F", 0x0F),
        new KeyValuePair<string, byte>("\\x10", 0x10),
        new KeyValuePair<string, byte>("\\x11", 0x11),
        new KeyValuePair<string, byte>("\\x12", 0x12),
        new KeyValuePair<string, byte>("\\x13", 0x13),
        new KeyValuePair<string, byte>("\\x14", 0x14),
        new KeyValuePair<string, byte>("\\x15", 0x15),
        new KeyValuePair<string, byte>("\\x16", 0x16),
        new KeyValuePair<string, byte>("\\x17", 0x17),
        new KeyValuePair<string, byte>("\\x18", 0x18),
        new KeyValuePair<string, byte>("\\x19", 0x19),
        new KeyValuePair<string, byte>("\\x1A", 0x1A),
        new KeyValuePair<string, byte>("\\e", 0x1B),
        new KeyValuePair<string, byte>("\\x1C", 0x1C),
        new KeyValuePair<string, byte>("\\x1D", 0x1D),
        new KeyValuePair<string, byte>("\\x1E", 0x1E),
        new KeyValuePair<string, byte>("\\x1F", 0x1F),
        new KeyValuePair<string, byte>(" ", 0x20),
        new KeyValuePair<string, byte>("!", 0x21),
        new KeyValuePair<string, byte>("\"", 0x22),
        new KeyValuePair<string, byte>("#", 0x23),
        new KeyValuePair<string, byte>("$", 0x24),
        new KeyValuePair<string, byte>("%", 0x25),
        new KeyValuePair<string, byte>("&", 0x26),
        new KeyValuePair<string, byte>("'", 0x27),
        new KeyValuePair<string, byte>("(", 0x28),
        new KeyValuePair<string, byte>(")", 0x29),
        new KeyValuePair<string, byte>("*", 0x2A),
        new KeyValuePair<string, byte>("+", 0x2B),
        new KeyValuePair<string, byte>(",", 0x2C),
        new KeyValuePair<string, byte>("-", 0x2D),
        new KeyValuePair<string, byte>(".", 0x2E),
        new KeyValuePair<string, byte>("/", 0x2F),
        new KeyValuePair<string, byte>("0", 0x30),
        new KeyValuePair<string, byte>("1", 0x31),
        new KeyValuePair<string, byte>("2", 0x32),
        new KeyValuePair<string, byte>("3", 0x33),
        new KeyValuePair<string, byte>("4", 0x34),
        new KeyValuePair<string, byte>("5", 0x35),
        new KeyValuePair<string, byte>("6", 0x36),
        new KeyValuePair<string, byte>("7", 0x37),
        new KeyValuePair<string, byte>("8", 0x38),
        new KeyValuePair<string, byte>("9", 0x39),
        new KeyValuePair<string, byte>(":", 0x3A),
        new KeyValuePair<string, byte>(";", 0x3B),
        new KeyValuePair<string, byte>("<", 0x3C),
        new KeyValuePair<string, byte>("=", 0x3D),
        new KeyValuePair<string, byte>(">", 0x3E),
        new KeyValuePair<string, byte>("?", 0x3F),
        new KeyValuePair<string, byte>("@", 0x40),
        new KeyValuePair<string, byte>("A", 0x41),
        new KeyValuePair<string, byte>("B", 0x42),
        new KeyValuePair<string, byte>("C", 0x43),
        new KeyValuePair<string, byte>("D", 0x44),
        new KeyValuePair<string, byte>("E", 0x45),
        new KeyValuePair<string, byte>("F", 0x46),
        new KeyValuePair<string, byte>("G", 0x47),
        new KeyValuePair<string, byte>("H", 0x48),
        new KeyValuePair<string, byte>("I", 0x49),
        new KeyValuePair<string, byte>("J", 0x4A),
        new KeyValuePair<string, byte>("K", 0x4B),
        new KeyValuePair<string, byte>("L", 0x4C),
        new KeyValuePair<string, byte>("M", 0x4D),
        new KeyValuePair<string, byte>("N", 0x4E),
        new KeyValuePair<string, byte>("O", 0x4F),
        new KeyValuePair<string, byte>("P", 0x50),
        new KeyValuePair<string, byte>("Q", 0x51),
        new KeyValuePair<string, byte>("R", 0x52),
        new KeyValuePair<string, byte>("S", 0x53),
        new KeyValuePair<string, byte>("T", 0x54),
        new KeyValuePair<string, byte>("U", 0x55),
        new KeyValuePair<string, byte>("V", 0x56),
        new KeyValuePair<string, byte>("W", 0x57),
        new KeyValuePair<string, byte>("X", 0x58),
        new KeyValuePair<string, byte>("Y", 0x59),
        new KeyValuePair<string, byte>("Z", 0x5A),
        new KeyValuePair<string, byte>("[", 0x5B),
        new KeyValuePair<string, byte>("\\", 0x5C),
        new KeyValuePair<string, byte>("]", 0x5D),
        new KeyValuePair<string, byte>("^", 0x5E),
        new KeyValuePair<string, byte>("_", 0x5F),
        new KeyValuePair<string, byte>("`", 0x60),
        new KeyValuePair<string, byte>("a", 0x61),
        new KeyValuePair<string, byte>("b", 0x62),
        new KeyValuePair<string, byte>("c", 0x63),
        new KeyValuePair<string, byte>("d", 0x64),
        new KeyValuePair<string, byte>("e", 0x65),
        new KeyValuePair<string, byte>("f", 0x66),
        new KeyValuePair<string, byte>("g", 0x67),
        new KeyValuePair<string, byte>("h", 0x68),
        new KeyValuePair<string, byte>("i", 0x69),
        new KeyValuePair<string, byte>("j", 0x6A),
        new KeyValuePair<string, byte>("k", 0x6B),
        new KeyValuePair<string, byte>("l", 0x6C),
        new KeyValuePair<string, byte>("m", 0x6D),
        new KeyValuePair<string, byte>("n", 0x6E),
        new KeyValuePair<string, byte>("o", 0x6F),
        new KeyValuePair<string, byte>("p", 0x70),
        new KeyValuePair<string, byte>("q", 0x71),
        new KeyValuePair<string, byte>("r", 0x72),
        new KeyValuePair<string, byte>("s", 0x73),
        new KeyValuePair<string, byte>("t", 0x74),
        new KeyValuePair<string, byte>("u", 0x75),
        new KeyValuePair<string, byte>("v", 0x76),
        new KeyValuePair<string, byte>("w", 0x77),
        new KeyValuePair<string, byte>("x", 0x78),
        new KeyValuePair<string, byte>("y", 0x79),
        new KeyValuePair<string, byte>("z", 0x7A),
        new KeyValuePair<string, byte>("{", 0x7B),
        new KeyValuePair<string, byte>("|", 0x7C),
        new KeyValuePair<string, byte>("}", 0x7D),
        new KeyValuePair<string, byte>("~", 0x7E),
        new KeyValuePair<string, byte>("\x7f", 0x7F)
    ]);
}