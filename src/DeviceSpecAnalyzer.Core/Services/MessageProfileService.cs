using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Services;

public interface IMessageProfileService
{
    MessageProfile? GetProfile(string messageId);
    IEnumerable<MessageProfile> GetProfilesByCategory(MessageCategory category);
    IEnumerable<MessageProfile> GetProfilesByDirection(MessageDirection direction);
    IEnumerable<MessageProfile> GetConversationStarters();
    IEnumerable<MessageProfile> GetRelatedMessages(string messageId);
    string GetDirectionIcon(MessageDirection direction);
    string GetCategoryBadgeClass(MessageCategory category);
    Dictionary<string, string> ExtractKeyValues(ParsedMessage message);
    bool IsRequestResponsePair(string messageId1, string messageId2);
}

public class MessageProfileService : IMessageProfileService
{
    public MessageProfile? GetProfile(string messageId)
    {
        MessageDefinitions.POCT1A_MessageProfiles.TryGetValue(messageId, out var profile);
        return profile;
    }

    public IEnumerable<MessageProfile> GetProfilesByCategory(MessageCategory category)
    {
        return MessageDefinitions.POCT1A_MessageProfiles.Values
            .Where(p => p.Category == category);
    }

    public IEnumerable<MessageProfile> GetProfilesByDirection(MessageDirection direction)
    {
        return MessageDefinitions.POCT1A_MessageProfiles.Values
            .Where(p => p.Direction == direction);
    }

    public IEnumerable<MessageProfile> GetConversationStarters()
    {
        return MessageDefinitions.POCT1A_MessageProfiles.Values
            .Where(p => p.IsConversationStarter);
    }

    public IEnumerable<MessageProfile> GetRelatedMessages(string messageId)
    {
        var profile = GetProfile(messageId);
        if (profile == null) return Enumerable.Empty<MessageProfile>();

        return profile.RelatedMessages
            .Select(GetProfile)
            .Where(p => p != null)!;
    }

    public string GetDirectionIcon(MessageDirection direction)
    {
        return direction switch
        {
            MessageDirection.DeviceToSystem => "→",
            MessageDirection.SystemToDevice => "←",
            MessageDirection.Bidirectional => "↔",
            _ => ""
        };
    }

    public string GetCategoryBadgeClass(MessageCategory category)
    {
        return category switch
        {
            MessageCategory.BasicProfile => "bg-primary",
            MessageCategory.Directive => "bg-info", 
            MessageCategory.VendorSpecific => "bg-warning",
            _ => "bg-secondary"
        };
    }

    public Dictionary<string, string> ExtractKeyValues(ParsedMessage message)
    {
        var keyValues = new Dictionary<string, string>();
        var profile = GetProfile(message.MessageType);
        
        if (profile == null) return keyValues;

        foreach (var keyField in profile.KeyFields)
        {
            var value = FindFieldValue(message.Segments, keyField);
            if (!string.IsNullOrEmpty(value))
            {
                keyValues[keyField] = value;
            }
        }

        return keyValues;
    }

    public bool IsRequestResponsePair(string messageId1, string messageId2)
    {
        var profile1 = GetProfile(messageId1);
        var profile2 = GetProfile(messageId2);
        
        if (profile1 == null || profile2 == null) return false;

        return profile1.RelatedMessages.Contains(messageId2) || 
               profile2.RelatedMessages.Contains(messageId1);
    }

    private string? FindFieldValue(List<MessageSegment> segments, string fieldId)
    {
        foreach (var segment in segments)
        {
            var field = segment.Fields.FirstOrDefault(f => 
                f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase) ||
                f.Name.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
                
            if (field != null && !string.IsNullOrWhiteSpace(field.Value))
            {
                return field.Value;
            }
        }
        return null;
    }
}