namespace MajdataEdit.Utils;

public class SubDivide
{
    public static string Subdivide(string phrase, float multiplier)
    {
        if (multiplier <= 1)
        {
            return phrase; // Invalid multiplier, return original phrase
        }

        int startIndex = phrase.IndexOf('{') + 1;
        int endIndex = phrase.IndexOf('}');

        if (startIndex == 0 || endIndex == -1 || endIndex <= startIndex)
        {
            return phrase; // Invalid format, return original phrase
        }

        // Extract the subdivision number and content inside the brackets
        int originalSubdivision = int.Parse(phrase[startIndex..endIndex]);
        string content = phrase[(endIndex + 1)..];

        // Calculate the new subdivision (round to nearest integer)
        int newSubdivision = (int)Math.Round(originalSubdivision * multiplier);

        // Process only the commas in the content
        var expandedContent = new System.Text.StringBuilder();
        double fractionalComma = 0.0;

        foreach (char c in content)
        {
            if (c == ',')
            {
                fractionalComma += multiplier;
                int repeatCount = (int)fractionalComma;
                fractionalComma -= repeatCount;
                expandedContent.Append(new string(',', repeatCount));
            }
            else
            {
                expandedContent.Append(c);
            }
        }

        return $"{{{newSubdivision}}}{expandedContent}";
    }
}
