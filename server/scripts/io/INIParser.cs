using System.Text;

namespace Max35050.IO;
public static class INIParser
{
    public static Dictionary<string, Dictionary<string, string>> ReadINI(string content)
    {
        Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();
        string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        string currentSection = "";
        foreach (string line in lines)
        {
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2);
                result[currentSection] = new Dictionary<string, string>();
            }
            else if (line.Contains("="))
            {
                string[] parts = line.Split(new[] { '=' }, 2);
                result[currentSection][parts[0].Trim()] = parts[1].Trim();
            }
        }
        return result;
    }
    public static INIContent INIToContent(string content)
    {
        return new INIContent(content);
    }
    public static string WriteINI(Dictionary<string, Dictionary<string, string>> data)
    {
        StringBuilder result = new StringBuilder();
        foreach (KeyValuePair<string, Dictionary<string, string>> section in data)
        {
            result.AppendLine("");
            result.AppendLine("[" + section.Key + "]");
            foreach (KeyValuePair<string, string> pair in section.Value)
            {
                result.AppendLine(pair.Key + "=" + pair.Value);
            }
        }
        try
        {
            result = result.Remove(0, 2); // Remove the first two newlines
        }
        catch
        {
            // Ignore
        }
        return result.ToString();
    }
    public static string WriteINI(INIContent content)
    {
        return WriteINI(content.Data);
    }
}
public class INIContent
{
    public Dictionary<string, Dictionary<string, string>> Data { get; set; }
    public INIContent(string content)
    {
        Data = INIParser.ReadINI(content);
    }
    public INIContent()
    {
        Data = new Dictionary<string, Dictionary<string, string>>();
    }
    public string? Get(string section, string key)
    {
        if (Data.ContainsKey(section) && Data[section].ContainsKey(key))
        {
            return Data[section][key];
        }
        return null;
    }
    public void Set(string section, string key, string value)
    {
        if (!Data.ContainsKey(section))
        {
            Data[section] = new Dictionary<string, string>();
        }
        Data[section][key] = value;
    }
    public void Remove(string section, string key)
    {
        if (Data.ContainsKey(section) && Data[section].ContainsKey(key))
        {
            Data[section].Remove(key);
        }
    }
    public void RemoveSection(string section)
    {
        if (Data.ContainsKey(section))
        {
            Data.Remove(section);
        }
    }
    public Dictionary<string, string>? GetSection(string section)
    {
        if (Data.ContainsKey(section))
        {
            return Data[section];
        }
        return null;
    }
    public override string ToString()
    {
        return INIParser.WriteINI(Data);
    }
}