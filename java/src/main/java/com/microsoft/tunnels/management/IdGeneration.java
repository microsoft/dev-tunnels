package com.microsoft.tunnels.management;

import com.microsoft.tunnels.contracts.TunnelConstraints;
import java.util.Random;

public class IdGeneration {
    private static String[] nouns = {"pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "rabbit", "shoe", "campsite", "plane", "sofa", "chair", "library", "book", "ocean", "lake", "river", "horse"};
    private static String[] adjectives = {"fun", "happy", "interesting", "neat", "peaceful", "puzzeled", "thoughtful", "kind", "joyful", "overjoyed", "new", "giant", "sneaky", "quick", "majestic", "gleaming", "jolly", "fancy", "tidy", "marvelous", "glamorous", "swift", "silent", "amusing", "spiffy"};
    private static Random rand = new Random();

    public static String generateTunnelId() {
        String tunnelId = "";
        tunnelId += adjectives[rand.nextInt(adjectives.length)] + "-";
        tunnelId += nouns[rand.nextInt(nouns.length)] + "-";
        for (int i = 0; i < 7; i++) {
            tunnelId += TunnelConstraints.oldTunnelIdChars.charAt(rand.nextInt(TunnelConstraints.oldTunnelIdChars.length()));
        }
        return tunnelId;
    }
}
