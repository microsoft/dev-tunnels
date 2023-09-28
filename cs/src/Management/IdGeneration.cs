using System.Text;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Static class that can be used for generating tunnelIds
/// </summary>
public static class IdGeneration
{
    private static string[] nouns = { "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "rabbit", "shoe", "campsite", "plane", "cake", "sofa", "chair", "library", "book", "ocean", "lake", "river" , "horse" };
    private static string[] adjectives = { "silly", "fun", "happy", "interesting", "neat", "peaceful", "puzzeled", "thoughtful", "kind", "joyful", "overjoyed", "new", "giant", "sneaky", "quick", "majestic", "gleaming", "jolly" , "fancy", "tidy", "marvelous", "glamorous", "swift", "silent", "amusing", "spiffy",  };
    /// <summary>
    /// Generate valid tunnelIds
    /// </summary>
    /// <returns>string tunnel id</returns>
    public static string GenerateTunnelId()
    {
        var sb = new StringBuilder();
        sb.Append(adjectives[ThreadSafeRandom.Next(adjectives.Length)]);
        sb.Append("-");
        sb.Append(nouns[ThreadSafeRandom.Next(nouns.Length)]);
        sb.Append("-");

        for (int i = 0; i < 7; i++)
        {
            sb.Append(TunnelConstraints.NewTunnelIdChars[ThreadSafeRandom.Next(TunnelConstraints.NewTunnelIdChars.Length)-1]);
        }
        return sb.ToString();
    }
}