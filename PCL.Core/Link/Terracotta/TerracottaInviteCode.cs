using System;

namespace PCL.Core.Link.Terracotta;

/// <summary>
///     陶瓦联机（Terracotta）邀请码的本地解析与校验。
/// </summary>
/// <remarks>
///     <para>
///         规则对齐陶瓦官方 v0.4.2 的 <c>src/controller/rooms/scaffolding/room.rs</c>，
///         目的是在把邀请码交给陶瓦之前先在本地挡掉明显非法的输入，
///         避免对 <c>GET /state/guesting</c> 产生注定返回 400 的无效请求。
///     </para>
///     <para>
///         邀请码形如 <c>U/XXXX-XXXX-XXXX-XXXX</c>：固定 21 个字符，
///         采用不含 <c>I</c> 与 <c>O</c> 的 34 进制字符表；
///         解析时容错地把 <c>I</c> 视作 <c>1</c>、<c>O</c> 视作 <c>0</c>，
///         并要求所得数值能被 7 整除（生成端以 <c>value - value % 7</c> 保证这一点）。
///     </para>
///     <para>
///         这些常量与陶瓦官方实现必须保持一致，任何改动都会导致互不兼容。
///     </para>
/// </remarks>
public static class TerracottaInviteCode
{
    /// <summary>邀请码使用的 34 进制字符表，刻意排除了容易与数字混淆的 I 与 O。</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>邀请码的固定总长度，即 <c>U/XXXX-XXXX-XXXX-XXXX</c>。</summary>
    public const int TotalLength = 21;

    /// <summary>去掉前缀与分隔符后的有效位数。</summary>
    private const int DigitCount = 16;

    /// <summary>邀请码前缀。</summary>
    private const string Prefix = "U/";

    /// <summary>进制基数，与 <see cref="Alphabet"/> 的长度一致。</summary>
    private const uint Radix = 34u;

    /// <summary>
    ///     尝试解析一个邀请码。
    /// </summary>
    /// <param name="input">用户输入的原始文本，允许夹杂空格与大小写差异。</param>
    /// <param name="normalized">
    ///     解析成功时为规范化的邀请码；失败时为空字符串。
    /// </param>
    /// <returns>邀请码合法时返回 <c>true</c>。</returns>
    public static bool TryParse(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var text = input.Trim().ToUpperInvariant();

        // 陶瓦自身允许邀请码出现在一段文字的中间（room.rs 用滑动窗口匹配），
        // 这里采取更保守的策略：只在 "U/" 标记之后取固定长度。
        var start = text.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0) return false;
        if (text.Length - start < TotalLength) return false;
        var candidate = text.Substring(start, TotalLength);

        Span<char> digits = stackalloc char[DigitCount];
        var count = 0;
        for (var i = Prefix.Length; i < candidate.Length; i++)
        {
            var c = candidate[i];
            if (c == '-') continue;
            switch (c)
            {
                case 'I': c = '1'; break;
                case 'O': c = '0'; break;
            }
            if (Alphabet.IndexOf(c) < 0) return false;
            if (count >= DigitCount) return false;
            digits[count++] = c;
        }
        if (count != DigitCount) return false;

        var value = UInt128.Zero;
        foreach (var c in digits) value = value * Radix + (uint)Alphabet.IndexOf(c);

        // 生成端保证数值是 7 的倍数，因此不是 7 的倍数即可判定为非法。
        if (value % 7 != 0) return false;

        normalized = Format(digits);
        return true;
    }

    /// <summary>
    ///     判断一个邀请码是否合法。
    /// </summary>
    public static bool IsValid(string? input) => TryParse(input, out _);

    /// <summary>把 16 位有效字符重新排版成带前缀与分隔符的规范形式。</summary>
    private static string Format(ReadOnlySpan<char> digits)
    {
        Span<char> buffer = stackalloc char[TotalLength];
        buffer[0] = 'U';
        buffer[1] = '/';
        var w = 2;
        for (var i = 0; i < DigitCount; i++)
        {
            if (i > 0 && i % 4 == 0) buffer[w++] = '-';
            buffer[w++] = digits[i];
        }
        return new string(buffer);
    }
}
