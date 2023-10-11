package com.microsoft.tunnels.management;

import com.microsoft.tunnels.contracts.TunnelConstraints;
import java.util.Random;

public class IdGeneration {
    private static String[] nouns = { "pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "shoe", "plane", "chair", "book", "ocean", "lake", "river" , "horse" };
    private static String[] adjectives = {"fun", "happy", "interesting", "neat", "peaceful", "puzzled", "kind", "joyful", "new", "giant", "sneaky", "quick", "majestic", "jolly" , "fancy", "tidy", "swift", "silent", "amusing", "spiffy"};
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
