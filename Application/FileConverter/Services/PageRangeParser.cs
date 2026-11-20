// <copyright file="PageRangeParser.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Parses 1-based user page range strings (e.g. "1-3, 5, 8-end") into 0-based page index collections.
    /// </summary>
    public static class PageRangeParser
    {
        public static List<int> ParseZeroBasedPageIndices(string rangeSpec, int pageCount)
        {
            if (pageCount <= 0)
            {
                return new List<int>();
            }

            if (string.IsNullOrWhiteSpace(rangeSpec))
            {
                return Enumerable.Range(0, pageCount).ToList();
            }

            var result = new List<int>();
            var seen = new HashSet<int>();

            string[] tokens = rangeSpec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawToken in tokens)
            {
                string token = rawToken.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                int dashIdx = token.IndexOf('-');
                if (dashIdx < 0)
                {
                    if (TryParseSinglePage(token, pageCount, out int pageOneBased))
                    {
                        int zero = pageOneBased - 1;
                        if (seen.Add(zero))
                        {
                            result.Add(zero);
                        }
                    }

                    continue;
                }

                string startPart = token.Substring(0, dashIdx).Trim();
                string endPart = token.Substring(dashIdx + 1).Trim();

                if (!TryParseSinglePage(startPart, pageCount, out int startOneBased))
                {
                    continue;
                }

                int endOneBased;
                if (string.IsNullOrEmpty(endPart) || string.Equals(endPart, "end", StringComparison.OrdinalIgnoreCase))
                {
                    endOneBased = pageCount;
                }
                else if (!TryParseSinglePage(endPart, pageCount, out endOneBased))
                {
                    continue;
                }

                int step = startOneBased <= endOneBased ? 1 : -1;
                for (int p = startOneBased; step > 0 ? p <= endOneBased : p >= endOneBased; p += step)
                {
                    int zero = p - 1;
                    if (seen.Add(zero))
                    {
                        result.Add(zero);
                    }
                }
            }

            return result;
        }

        public static List<List<int>> ParseRangeGroups(string rangeSpec, int pageCount)
        {
            var groups = new List<List<int>>();
            if (pageCount <= 0)
            {
                return groups;
            }

            if (string.IsNullOrWhiteSpace(rangeSpec))
            {
                groups.Add(Enumerable.Range(0, pageCount).ToList());
                return groups;
            }

            string[] tokens = rangeSpec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawToken in tokens)
            {
                List<int> sub = ParseZeroBasedPageIndices(rawToken, pageCount);
                if (sub.Count > 0)
                {
                    groups.Add(sub);
                }
            }

            return groups;
        }

        private static bool TryParseSinglePage(string token, int pageCount, out int pageOneBased)
        {
            if (string.Equals(token, "end", StringComparison.OrdinalIgnoreCase))
            {
                pageOneBased = pageCount;
                return true;
            }

            if (int.TryParse(token, out int parsed))
            {
                pageOneBased = Math.Max(1, Math.Min(pageCount, parsed));
                return true;
            }

            pageOneBased = 1;
            return false;
        }
    }
}
