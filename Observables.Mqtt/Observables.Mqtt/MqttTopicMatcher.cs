namespace Observables.Mqtt;

internal static class MqttTopicMatcher
{
    const string MultiLevelWildcard = "#";
    const string SingleLevelWildcard = "+";

    public static bool Matches(string filter, string? topic)
    {
        if (topic is null)
        {
            return false;
        }

        // MQTT-4.7.2-1: a filter that begins with + or # must not match a topic that begins with $.
        if (topic.Length > 0
            && topic[0] == '$'
            && filter.Length > 0
            && (filter[0] == '#' || filter[0] == '+'))
        {
            return false;
        }

        var filterParts = filter.Split('/');
        var topicParts = topic.Split('/');
        for (var i = 0; i < filterParts.Length; i++)
        {
            var fp = filterParts[i];
            if (fp == MultiLevelWildcard)
            {
                return true;
            }

            if (i >= topicParts.Length)
            {
                return false;
            }

            if (fp != SingleLevelWildcard && fp != topicParts[i])
            {
                return false;
            }
        }

        return filterParts.Length == topicParts.Length;
    }
}
