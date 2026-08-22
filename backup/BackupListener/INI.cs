using System.Text;
public class INI
{
    private readonly Dictionary<string, string> _global = new();
    private readonly Dictionary<string, Dictionary<string, string>> _sectioned = new();

    public INI(string content)
    {
        string currentHeader = "";

        var lines = content.Split('\n');

        foreach (string rawLine in lines)
        {
            string l = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(l))
                continue;

            if (l.StartsWith(";"))
                continue;

            if (l.StartsWith("[") && l.EndsWith("]"))
            {
                currentHeader = l[1..^1].Trim();

                if (!_sectioned.ContainsKey(currentHeader))
                    _sectioned[currentHeader] = new Dictionary<string, string>();

                continue;
            }

            int i = l.IndexOf('=');
            if (i < 0)
                continue;

            string key = l[..i].Trim();
            string value = l[(i + 1)..]
                .Trim()
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\");

            if (currentHeader == "")
                _global[key] = value;
            else
                _sectioned[currentHeader][key] = value;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var g in _global)
        {
            sb.Append(g.Key)
              .Append('=')
              .Append(g.Value.Replace("\\", "\\\\").Replace("\n", "\\n"))
              .Append('\n');
        }

        foreach (var section in _sectioned)
        {
            sb.Append('[').Append(section.Key).Append("]\n");

            foreach (var v in section.Value)
            {
                sb.Append(v.Key)
                  .Append('=')
                  .Append(v.Value.Replace("\\", "\\\\").Replace("\n", "\\n"))
                  .Append('\n');
            }
        }

        return sb.ToString();
    }

    public string ReadString(string key) => _global[key];

    public string ReadString(string header, string key, string v) => _sectioned[header][key];
    public string[] ReadStringArray(string key)
    {
        if (!_global.TryGetValue(key, out string? raw))
            return Array.Empty<string>();

        raw = raw.Trim();

        if (!raw.StartsWith("[") || !raw.EndsWith("]"))
            return Array.Empty<string>();

        raw = raw[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                current.Append(next);
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result.ToArray();
    }
    public string[] ReadStringArray(string header, string key)
    {
        if (!_sectioned.TryGetValue(header, out var section) || !section.TryGetValue(key, out string? raw))
            return Array.Empty<string>();

        raw = raw.Trim();

        if (!raw.StartsWith("[") || !raw.EndsWith("]"))
            return Array.Empty<string>();

        raw = raw[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                current.Append(next);
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result.ToArray();
    }
    public void WriteString(string key, string value) => _global[key] = value;

    public void WriteString(string header, string key, string value)
    {
        if (!_sectioned.ContainsKey(header))
            _sectioned[header] = new Dictionary<string, string>();

        _sectioned[header][key] = value;
    }
    public void WriteStringArray(string key, string[] values)
    {
        if (values == null)
        {
            _global[key] = "[]";
            return;
        }

        var sb = new StringBuilder();
        sb.Append('[');

        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(',');

            string v = values[i] ?? "";

            sb.Append('"')
            .Append(v.Replace("\\", "\\\\").Replace("\"", "\\\""))
            .Append('"');
        }

        sb.Append(']');

        _global[key] = sb.ToString();
    }
    public void WriteStringArray(string header, string key, string[] values)
    {
        if (!_sectioned.ContainsKey(header))
            _sectioned[header] = new Dictionary<string, string>();

        if (values == null)
        {
            _sectioned[header][key] = "[]";
            return;
        }

        var sb = new StringBuilder();
        sb.Append('[');

        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(',');

            string v = values[i] ?? "";

            sb.Append('"')
            .Append(v.Replace("\\", "\\\\").Replace("\"", "\\\""))
            .Append('"');
        }

        sb.Append(']');

        _sectioned[header][key] = sb.ToString();
    }
    public bool TryReadString(string key, out string? value) => _global.TryGetValue(key, out value);
    public bool TryReadString(string header, string key, out string? value)
    {
        value = null;
        return _sectioned.TryGetValue(header, out var section) && section.TryGetValue(key, out value);
    }
    public bool TryReadStringArray(string key, out string[]? values)
    {
        values = null;

        if (!_global.TryGetValue(key, out string? raw))
            return false;

        raw = raw.Trim();

        if (!raw.StartsWith("[") || !raw.EndsWith("]"))
            return false;

        raw = raw[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(raw))
        {
            values = Array.Empty<string>();
            return true;
        }

        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                current.Append(next);
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        values = result.ToArray();
        return true;
    }
    public bool TryReadStringArray(string header, string key, out string[]? values)
    {
        values = null;

        if (!_sectioned.TryGetValue(header, out var section) || !section.TryGetValue(key, out string? raw))
            return false;

        raw = raw.Trim();

        if (!raw.StartsWith("[") || !raw.EndsWith("]"))
            return false;

        raw = raw[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(raw))
        {
            values = Array.Empty<string>();
            return true;
        }

        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '\\' && i + 1 < raw.Length)
            {
                char next = raw[++i];
                current.Append(next);
                continue;
            }

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        values = result.ToArray();
        return true;
    }
    public void RemoveKey(string key) => _global.Remove(key);
    public void RemoveKey(string header, string key)
    {
        if (_sectioned.TryGetValue(header, out var section))
            section.Remove(key);
    }
    public bool ContainsKey(string key) => _global.ContainsKey(key);
    public bool ContainsKey(string header, string key) => _sectioned.TryGetValue(header, out var section) && section.ContainsKey(key);
}