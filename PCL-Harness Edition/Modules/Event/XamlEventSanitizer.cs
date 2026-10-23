using System.Text.RegularExpressions;

namespace PCL
{
    public static partial class XamlEventSanitizer
    {
        public class SanitizeResult
        {
            public string SanitizedXaml { get; set; } = "";
            public List<string> UnsupportedTypesFound { get; set; } = new();
            public List<string> UnrecognizedTypes { get; set; } = new();
        }

        [GeneratedRegex(@"(local:CustomEventService\.EventType\s*=\s*"")([^""]+)("")")]
        private static partial Regex EventTypeAttributeRegex();

        [GeneratedRegex(@"(<local:CustomEventService\.EventType\s*>\s*)([^<]+?)(\s*</local:CustomEventService\.EventType\s*>)")]
        private static partial Regex EventTypePropertyElementRegex();

        [GeneratedRegex(@"(<local:CustomEvent\.Type\s*>\s*)([^<]+?)(\s*</local:CustomEvent\.Type\s*>)")]
        private static partial Regex CustomEventTypePropertyElementRegex();

        [GeneratedRegex(@"(<local:CustomEvent\s+[^>]*?\bType\s*=\s*"")([^""]+)("")")]
        private static partial Regex LocalCustomEventTypeAttributeRegex();

        [GeneratedRegex(@"<local:CustomEvent\s+[^>]*?\bType\s*=\s*""([^""]+)""[^>]*/\s*>")]
        private static partial Regex LocalCustomEventSelfClosingRegex();

        [GeneratedRegex(@"<local:CustomEvent\s+[^>]*?\bType\s*=\s*""([^""]+)""[^>]*>")]
        private static partial Regex LocalCustomEventOpenTagRegex();

        /// <summary>
        /// 「成对开标签 + 子内容 + 闭标签」形式的 local:CustomEvent 元素，用于整段移除坏类型元素。
        /// 注意不要退化回只匹配开标签：那样替换后闭标签会孤立残留。
        /// </summary>
        [GeneratedRegex(@"<local:CustomEvent\s+[^>]*?\bType\s*=\s*""([^""]+)""[^>]*>[\s\S]*?</local:CustomEvent\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex LocalCustomEventPairedRegex();

        [GeneratedRegex(@"<Setter\b[^>]*?\bProperty\s*=\s*""local:CustomEventService\.EventType""[^>]*?\bValue\s*=\s*""([^""]+)""[^>]*(?:/\s*>|>\s*</Setter\s*>)")]
        private static partial Regex SetterEventTypePropertyFirstRegex();

        [GeneratedRegex(@"<Setter\b[^>]*?\bValue\s*=\s*""([^""]+)""[^>]*?\bProperty\s*=\s*""local:CustomEventService\.EventType""[^>]*(?:/\s*>|>\s*</Setter\s*>)")]
        private static partial Regex SetterEventTypeValueFirstRegex();

        public static SanitizeResult Sanitize(string xaml)
        {
            var result = new SanitizeResult();
            var sanitized = xaml;

            sanitized = EventTypeAttributeRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value;
                return ReplaceEventType(match, rawValue, result);
            });

            sanitized = EventTypePropertyElementRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value.Trim();
                return ReplaceEventType(match, rawValue, result);
            });

            sanitized = CustomEventTypePropertyElementRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value.Trim();
                return ReplaceOrRemovePropertyElement(match, rawValue, result);
            });

            sanitized = LocalCustomEventTypeAttributeRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[2].Value;
                return ReplaceEventType(match, rawValue, result);
            });

            sanitized = LocalCustomEventSelfClosingRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[1].Value;
                return _RemoveBadCustomEventElement(match, rawValue, result);
            });

            // 「成对开标签 + 子内容」形式的坏类型元素必须整段移除。原实现只匹配了开标签，
            // 替换串却返回「闭标签之后的尾部」，结果是孤立闭标签残留、尾部内容被重复插入，
            // 拼出非法 XAML 后交给 GetObjectFromXML 解析必然失败。这里改用覆盖整个元素的匹配。
            sanitized = LocalCustomEventPairedRegex().Replace(sanitized, match =>
            {
                var rawValue = match.Groups[1].Value;
                return _IsBadType(rawValue, result) ? "" : match.Value;
            });

            sanitized = SetterEventTypePropertyFirstRegex().Replace(sanitized, match =>
                _ReplaceSetter(match, match.Groups[1].Value, result));

            sanitized = SetterEventTypeValueFirstRegex().Replace(sanitized, match =>
                _ReplaceSetter(match, match.Groups[1].Value, result));

            RemoveElementsForTypes(result.UnsupportedTypesFound, ref sanitized);
            RemoveElementsForTypes(result.UnrecognizedTypes, ref sanitized);

            result.UnsupportedTypesFound = result.UnsupportedTypesFound.Distinct().ToList();
            result.UnrecognizedTypes = result.UnrecognizedTypes.Distinct().ToList();

            result.SanitizedXaml = sanitized;
            return result;
        }

        private static bool _IsBadType(string rawValue, SanitizeResult result)
        {
            if (EventTypeMapper.IsUnsupportedType(rawValue))
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return true;
            }
            if (!Enum.TryParse<EventType>(rawValue, true, out _)
                && !EventTypeMapper.TryToEnglish(rawValue, out _))
            {
                result.UnrecognizedTypes.Add(rawValue);
                return true;
            }
            return false;
        }

        private static string _RemoveBadCustomEventElement(
            Match match, string rawValue, SanitizeResult result)
        {
            return _IsBadType(rawValue, result) ? "" : match.Value;
        }

        private static void RemoveElementsForTypes(List<string> types, ref string sanitized)
        {
            var snapshot = types.ToList();
            foreach (var type in snapshot)
                sanitized = RemoveElementsWithEventType(sanitized, type, types);
        }

        private static string ReplaceEventType(Match match, string rawValue, SanitizeResult result)
        {
            if (EventTypeMapper.TryToEnglish(rawValue, out var englishName))
                return match.Groups[1].Value + englishName + match.Groups[3].Value;

            if (Enum.TryParse<EventType>(rawValue, true, out _))
                return match.Value;

            if (EventTypeMapper.IsUnsupportedType(rawValue))
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return match.Value;
            }

            result.UnrecognizedTypes.Add(rawValue);
            return match.Value;
        }

        private static string ReplaceOrRemovePropertyElement(
            Match match, string rawValue, SanitizeResult result)
        {
            if (_IsBadType(rawValue, result))
                return "";
            return ReplaceEventType(match, rawValue, result);
        }

        private static string _ReplaceSetter(Match match, string rawValue, SanitizeResult result)
        {
            if (_IsBadType(rawValue, result))
                return "";
            if (EventTypeMapper.TryToEnglish(rawValue, out var englishName))
                return match.Value.Replace($"Value=\"{rawValue}\"", $"Value=\"{englishName}\"");
            return match.Value;
        }

        private static string RemoveElementsWithEventType(string xaml, string eventTypeValue, List<string> trackingList)
        {
            var escaped = Regex.Escape(eventTypeValue);

            var selfClosingPattern = $@"<[\w:]+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*/\s*>";
            xaml = Regex.Replace(xaml, selfClosingPattern, match =>
            {
                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            // 同 :73 的说明：必须把整个元素（含子内容与闭标签）一次性吃掉，
            // 只匹配开标签会让闭标签孤立残留、尾部重复插入，拼出非法 XAML。
            var pairedPattern =
                $@"<([\w:]+)[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*>[\s\S]*?</\1\s*>";
            xaml = Regex.Replace(xaml, pairedPattern, match =>
            {
                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            var propertyElementPattern = $@"<local:CustomEventService\.EventType\s*>\s*{escaped}\s*</local:CustomEventService\.EventType\s*>";
            xaml = Regex.Replace(xaml, propertyElementPattern, match =>
            {
                var beforeMatch = xaml[..match.Index];
                var lastOpenMatch = Regex.Match(beforeMatch, @"<([\w:]+)[^>]*>\s*$", RegexOptions.RightToLeft);
                if (!lastOpenMatch.Success) return match.Value;
                var parentElementName = lastOpenMatch.Groups[1].Value;

                var afterProperty = xaml[(match.Index + match.Length)..];
                var parentCloseMatch = Regex.Match(afterProperty, $@"</{parentElementName}\s*>");
                if (!parentCloseMatch.Success) return match.Value;

                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            return xaml;
        }

        private static int FindMatchingCloseTag(string text, string elementName)
        {
            var closePattern = $@"</{elementName}\s*>";
            var closeMatch = Regex.Match(text, closePattern);
            return closeMatch.Success ? closeMatch.Index + closeMatch.Length : -1;
        }
    }
}
