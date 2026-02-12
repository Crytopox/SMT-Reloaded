using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HISA.EVEData;

namespace HISA.Helpers;

public static class IntelReportAnalyzer
{
    private static readonly Regex IntelPlusCountRegex = new Regex(@"(?:^|[\s,;:])\+(\d{1,3})(?=$|[\s,;:.!?])", RegexOptions.Compiled);
    private static readonly Regex IntelStandaloneCountRegex = new Regex(@"(?:^|[\s,;:])(\d{1,3})(?=$|[\s,;:.!?])", RegexOptions.Compiled);
    private static readonly Regex IntelNameTokenRegex = new Regex(@"[A-Za-z0-9'._\-]+", RegexOptions.Compiled);
    private static readonly Regex IntelCountContextRegex = new Regex(@"\b(hostile|hostiles|neut|neuts|enemy|enemies|fleet|gang|local|inbound|outbound|plus|spike|spiked)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IntelLargeEnemyGroupRegex = new Regex(@"\b(gang|spike|spiked)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> IntelCountStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hostile", "hostiles", "neut", "neuts", "neutral", "enemy", "enemies", "fleet", "gang", "clear", "clr",
        "local", "status", "reported", "report", "intel", "dscan", "scan", "camp", "gate", "in", "on", "at",
        "to", "from", "with", "plus", "is", "are", "was", "were", "spike", "spiked", "ship", "ships", "pilot", "pilots",
        "frigate", "destroyer", "cruiser", "battlecruiser", "battleship", "industrial", "freighter", "capital", "fighter",
        "mining", "structure", "capsule", "pod", "pods", "cyno", "bubble", "dictor", "hictor", "logi", "jump", "inbound", "outbound"
    };

    public static string GetShipClassLabel(IntelShipClass shipClass)
    {
        switch(shipClass)
        {
            case IntelShipClass.UnknownHostile: return "Hostile (unknown class)";
            case IntelShipClass.Capsule: return "Capsule";
            case IntelShipClass.Frigate: return "Frigate";
            case IntelShipClass.Destroyer: return "Destroyer";
            case IntelShipClass.Cruiser: return "Cruiser";
            case IntelShipClass.Battlecruiser: return "Battlecruiser";
            case IntelShipClass.Battleship: return "Battleship";
            case IntelShipClass.Industrial: return "Industrial";
            case IntelShipClass.Mining: return "Mining";
            case IntelShipClass.Freighter: return "Freighter";
            case IntelShipClass.Capital: return "Capital";
            case IntelShipClass.Fighter: return "Fighter";
            case IntelShipClass.Structure: return "Structure";
            default:
                return "Hostile";
        }
    }

    public static string GetBadgeTooltip(IntelData intelData, IntelShipClass shipClass, bool overflowFighterFill, int maxBadges)
    {
        if(overflowFighterFill)
        {
            int hostileCount = EstimateHostileCount(intelData);
            return hostileCount > maxBadges
                ? $"Heavy hostile presence ({hostileCount} reported)"
                : "Heavy hostile presence";
        }

        string classText = GetShipClassLabel(shipClass);
        if(intelData?.ReportedShips == null || intelData.ReportedShips.Count == 0)
        {
            return classText;
        }

        int maxShown = 6;
        List<string> shownShips = intelData.ReportedShips.Take(maxShown).ToList();
        int overflow = intelData.ReportedShips.Count - shownShips.Count;
        return overflow > 0
            ? $"{classText}: {string.Join(", ", shownShips)} +{overflow} more"
            : $"{classText}: {string.Join(", ", shownShips)}";
    }

    public static List<IntelShipClass> BuildBadgeClasses(IntelData intelData, int maxBadges, out bool overflowFighterFill)
    {
        overflowFighterFill = false;

        if(intelData == null || intelData.ClearNotification)
        {
            return new List<IntelShipClass>();
        }

        int hostileCount = EstimateHostileCount(intelData);

        List<IntelShipClass> classPool = intelData.ReportedShipClasses?
            .Where(c => Enum.IsDefined(typeof(IntelShipClass), c))
            .Distinct()
            .ToList() ?? new List<IntelShipClass>();

        if(classPool.Count > 1)
        {
            classPool.Remove(IntelShipClass.UnknownHostile);
        }

        classPool = classPool
            .OrderBy(GetShipClassSortOrder)
            .ToList();

        if(classPool.Count == 0)
        {
            classPool.Add(IntelShipClass.UnknownHostile);
        }

        hostileCount = Math.Max(hostileCount, classPool.Count);

        if(hostileCount > maxBadges)
        {
            overflowFighterFill = true;
            return Enumerable.Repeat(IntelShipClass.Fighter, maxBadges).ToList();
        }

        List<IntelShipClass> badges = new List<IntelShipClass>(hostileCount);

        for(int i = 0; i < hostileCount; i++)
        {
            IntelShipClass shipClass = i < classPool.Count
                ? classPool[i]
                : classPool[(i - classPool.Count) % classPool.Count];
            badges.Add(shipClass);
        }

        return badges;
    }

    public static string FormatAge(DateTime intelTime)
    {
        TimeSpan age = DateTime.Now - intelTime;
        if(age.TotalSeconds < 0)
        {
            age = TimeSpan.Zero;
        }

        if(age.TotalMinutes < 1)
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s ago";
        }

        if(age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes}m {age.Seconds:00}s ago";
        }

        if(age.TotalDays < 1)
        {
            return $"{(int)age.TotalHours}h {age.Minutes:00}m ago";
        }

        return $"{(int)age.TotalDays}d {age.Hours:00}h ago";
    }

    public static string JoinValues(IEnumerable<string> values, int maxShown)
    {
        if(values == null)
        {
            return "None";
        }

        List<string> items = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if(items.Count == 0)
        {
            return "None";
        }

        if(items.Count <= maxShown)
        {
            return string.Join(", ", items);
        }

        return $"{string.Join(", ", items.Take(maxShown))} +{items.Count - maxShown}";
    }

    public static string CompactText(string intelText, int maxLength = 120)
    {
        if(string.IsNullOrWhiteSpace(intelText))
        {
            return "No text";
        }

        string compact = intelText.Trim();
        if(compact.Length <= maxLength)
        {
            return compact;
        }

        return compact.Substring(0, maxLength - 3) + "...";
    }

    public static int EstimateHostileCount(IntelData intelData)
    {
        if(intelData == null)
        {
            return 0;
        }

        int shipCount = intelData.ReportedShips?.Count ?? 0;
        int classCount = intelData.ReportedShipClasses?.Count(c => c != IntelShipClass.UnknownHostile) ?? 0;
        int pilotMentions = intelData.ReportedPilots?.Count ?? 0;
        if(pilotMentions == 0)
        {
            pilotMentions = EstimatePilotMentionsFromIntelText(intelData);
        }

        int baseCount = Math.Max(shipCount, Math.Max(classCount, pilotMentions));
        (int additionalFromPlus, int explicitStandaloneCount) = GetIntelCountHints(intelData.IntelString);

        int count;
        if(additionalFromPlus > 0)
        {
            count = baseCount + additionalFromPlus;
        }
        else if(explicitStandaloneCount > 0)
        {
            count = baseCount > 0
                ? baseCount + explicitStandaloneCount
                : explicitStandaloneCount;
        }
        else
        {
            count = baseCount;
        }

        if(!intelData.ClearNotification &&
           !string.IsNullOrWhiteSpace(intelData.IntelString) &&
           IntelLargeEnemyGroupRegex.IsMatch(intelData.IntelString))
        {
            count = Math.Max(count, 6);
        }

        if(!intelData.ClearNotification && count == 0)
        {
            count = 1;
        }

        return count;
    }

    private static int GetShipClassSortOrder(IntelShipClass shipClass)
    {
        switch(shipClass)
        {
            case IntelShipClass.Capsule: return 0;
            case IntelShipClass.Frigate: return 1;
            case IntelShipClass.Destroyer: return 2;
            case IntelShipClass.Cruiser: return 3;
            case IntelShipClass.Battlecruiser: return 4;
            case IntelShipClass.Battleship: return 5;
            case IntelShipClass.Industrial: return 6;
            case IntelShipClass.Mining: return 7;
            case IntelShipClass.Freighter: return 8;
            case IntelShipClass.Capital: return 9;
            case IntelShipClass.Fighter: return 10;
            case IntelShipClass.Structure: return 11;
            case IntelShipClass.UnknownHostile:
            default:
                return 100;
        }
    }

    private static (int plusAdditional, int explicitStandalone) GetIntelCountHints(string intelText)
    {
        if(string.IsNullOrWhiteSpace(intelText))
        {
            return (0, 0);
        }

        int plusAdditional = 0;
        foreach(Match m in IntelPlusCountRegex.Matches(intelText))
        {
            if(int.TryParse(m.Groups[1].Value, out int parsed) && parsed > 0 && parsed <= 250)
            {
                plusAdditional += parsed;
            }
        }

        List<int> standalone = new List<int>();
        foreach(Match m in IntelStandaloneCountRegex.Matches(intelText))
        {
            if(int.TryParse(m.Groups[1].Value, out int parsed) && parsed > 0 && parsed <= 250)
            {
                standalone.Add(parsed);
            }
        }

        int explicitStandalone = 0;
        if(standalone.Count > 0)
        {
            bool hasContext = IntelCountContextRegex.IsMatch(intelText);
            explicitStandalone = hasContext ? standalone.Sum() : standalone.Max();
        }

        return (plusAdditional, explicitStandalone);
    }

    private static int EstimatePilotMentionsFromIntelText(IntelData intelData)
    {
        if(intelData == null || string.IsNullOrWhiteSpace(intelData.IntelString))
        {
            return 0;
        }

        HashSet<string> matchedSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(intelData.Systems != null)
        {
            foreach(string systemName in intelData.Systems)
            {
                if(!string.IsNullOrWhiteSpace(systemName))
                {
                    matchedSystems.Add(systemName);
                }
            }
        }

        HashSet<string> shipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(intelData.ReportedShips != null)
        {
            foreach(string shipName in intelData.ReportedShips)
            {
                if(string.IsNullOrWhiteSpace(shipName))
                {
                    continue;
                }

                foreach(Match m in IntelNameTokenRegex.Matches(shipName))
                {
                    if(!string.IsNullOrWhiteSpace(m.Value))
                    {
                        shipWords.Add(m.Value);
                    }
                }
            }
        }

        List<string> tokens = IntelNameTokenRegex.Matches(intelData.IntelString)
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        bool IsCandidateNameToken(string token)
        {
            if(string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if(token.All(char.IsDigit))
            {
                return false;
            }

            if(!token.Any(char.IsLetter))
            {
                return false;
            }

            if(!char.IsUpper(token[0]))
            {
                return false;
            }

            if(IntelCountStopWords.Contains(token))
            {
                return false;
            }

            if(matchedSystems.Contains(token))
            {
                return false;
            }

            if(LooksLikeSystemStyleToken(token))
            {
                return false;
            }

            if(shipWords.Contains(token))
            {
                return false;
            }

            return true;
        }

        HashSet<string> nameMentions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if(!IsCandidateNameToken(token))
            {
                continue;
            }

            string mention = token;
            if(i + 1 < tokens.Count && IsCandidateNameToken(tokens[i + 1]))
            {
                mention = token + " " + tokens[i + 1];
                i++;
                if(i + 1 < tokens.Count && IsCandidateNameToken(tokens[i + 1]))
                {
                    mention += " " + tokens[i + 1];
                    i++;
                }
            }

            nameMentions.Add(mention);
        }

        return nameMentions.Count;
    }

    private static bool LooksLikeSystemStyleToken(string token)
    {
        if(string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        bool hasDigit = token.Any(char.IsDigit);
        bool hasHyphen = token.Contains('-');
        if(hasDigit && hasHyphen)
        {
            return true;
        }

        int systemLikeChars = token.Count(ch => char.IsUpper(ch) || char.IsDigit(ch) || ch == '-');
        return token.Length >= 4 && systemLikeChars == token.Length;
    }
}
