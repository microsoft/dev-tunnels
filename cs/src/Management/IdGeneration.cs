using System.Text;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Static class that can be used for generating tunnelIds
/// </summary>
public static class IdGeneration
{
    private static string[] nouns = { "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "shoe", "plane", "chair", "book", "ocean", "lake", "river" , "horse" };
    private static string[] adjectives = { "fun", "happy", "interesting", "neat", "peaceful", "puzzled", "kind", "joyful", "new", "giant", "sneaky", "quick", "majestic", "jolly" , "fancy", "tidy", "swift", "silent", "amusing", "spiffy" };
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
            sb.Append(TunnelConstraints.OldTunnelIdChars[ThreadSafeRandom.Next(TunnelConstraints.OldTunnelIdChars.Length)]);
        }
        return sb.ToString();
    }
}