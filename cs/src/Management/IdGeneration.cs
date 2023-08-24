using System;
using System.Text;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// Static class that can be used for generating tunnelIds
/// </summary>
public static class IdGeneration
{
    private static string[] words = {
        "flibbertigibbet", "gobbledygook", "wobble", "zoodle", "bazinga",
        "wubbulous", "quizzle", "jibberjabber", "snickersnee", "gibberish",
        "noodle", "malarkey", "gibber", "rhubarb", "wobblegobble",
        "hullabaloo", "brouhaha", "giggly", "fiddlefaddle", "whimsy",
        "jibber", "flibber", "piffle", "gobbledegook", "yabbadabbadoo",
        "wobblewabble", "doodle", "zany", "razzmatazz", "whatchamacallit",
        "gazump", "quixotic", "noodlehead", "blubber", "foofaraw",
        "gobbledygoo", "flummox", "wibblywobbly", "gibbering", "zoodlemania",
        "gibberer", "riffraff", "flibberflop", "fiddlesticks", "yaketyyak",
        "zibber", "doozy", "lollygag", "flibberflap", "brouhah",
        "gibbosity", "wibbly", "jibbering", "whatsit", "gobsmacked",
        "kerfuffle", "flibberty", "gobblers", "zibbering", "whizbang",
        "piddle", "wibblywobble", "gobbledy", "gibberize", "gibbered",
        "riffle", "snicker", "whatsis", "gibbergabber", "wibbled",
        "wobblegob", "quibble", "giblets", "gibberishly", "flabbergast",
        "noodlebrain", "gibberishness", "zibberzabber", "flibbering", "wobblezobble",
        "gobbledegookery", "dibble", "jibberty", "gibberflap", "noodlemania",
        "whatsahoosit", "wobblezoodle", "fizzgig", "gibberosity", "noodly",
        "flibbertigib", "gibberlings", "wobblegobbling", "zibberzoodle", "gobbledygoozle",
        "whatsahoozle", "gibbergobble", "fiddledeedee", "jibberwocky", "gobbledegeek",
        "flibberjib", "quizzlewump", "snickerdoodle", "blunderbuss", "gobbledygook", "lollygag", "wobbleflop",
        "noodlebrain", "zippityzap", "quibblesnort", "bumblebeeble", "pumpernickel", "wobblebonk", "snickersnee",
        "dingleberry", "skedaddle", "fiddlesticks", "ballyhoo", "wigglywoo", "gobsmacked", "ziggityzag", "wobblegobble",
        "quibberquack", "noodleoodle", "fluffernutter", "bunglebop", "skibberjank", "whatchamacallit", "wobbleplop",
        "snickerfritz", "blibberblab", "quokkadoodle", "gobbledoodle", "noodleoodle", "doozywhatsit", "skedaddleskoo",
        "flibbertigibbet", "bumbershoot", "wobblewham", "bazinga", "quibbleflap", "gobblersmack", "noodledoodle",
        "wobblefiddle", "zippitydoo", "fiddlefaddle", "bumbleflop", "skedoodle", "wobbleblib", "quizzlenoodle",
        "bunglewobble", "snickerwhack", "gobbledoodle", "flubberwham", "quibblesquawk", "wobblewump", "zippityflap",
        "noodlebop", "skibberflap", "flibberwobble", "bumbersnicker", "wobblebop", "quirkyquack", "gobbledoodle", "dingleflop",
        "snickersnort", "blibberwiggle", "noodledoodle", "wobblewhack", "zippityjib", "fiddleflap", "bunglebop",
        "skibberdoodle", "flubbernoodle", "quibblequack", "wobblewobble", "gobbleflop", "snickerdoodle", "bumbleflap",
        "quizzlesnort", "noodlewiggle", "skedoodle", "flibberwhack", "bumbersnort", "wobbleflap", "zippitydoodle",
        "gobbledoodle", "blibberbop", "quirkysnicker", "snickerbop", "noodlequack", "wobblefiddle", "dinglewobble",
        "skibberwhack", "flibberflap", "bunglequack", "quibblebop", "gobblewiggle", "zippitysnort", "wobbleflib",
        "flibberjibber", "quizzlewump", "snickerdoodle", "blunderbuss", "gobbledygook", "lollygag", "wobbleflop",
        "noodlebrain", "zippityzap", "quibblesnort", "bumblebeeble", "pumpernickel", "wobblebonk", "snickersnee",
        "dingleberry", "skedaddle", "fiddlesticks", "ballyhoo", "wigglywoo", "gobsmacked", "ziggityzag", "wobblegobble",
        "quibberquack", "noodleoodle", "fluffernutter", "bunglebop", "skibberjank", "whatchamacallit", "wobbleplop",
        "snickerfritz", "blibberblab", "quokkadoodle", "gobbledoodle", "noodleoodle", "doozywhatsit", "skedaddleskoo",
        "flibbertigibbet", "bumbershoot", "wobblewham", "bazinga", "quibbleflap", "gobblersmack", "noodledoodle",
        "wobblefiddle", "zippitydoo", "fiddlefaddle", "bumbleflop", "skedoodle", "wobbleblib", "quizzlenoodle",
        "bunglewobble", "snickerwhack", "gobbledoodle", "flubberwham", "quibblesquawk", "wobblewump", "zippityflap",
        "noodlebop", "skibberflap", "flibberwobble", "bumbersnicker", "wobblebop", "quirkyquack", "gobbledoodle", "dingleflop",
        "snickersnort", "blibberwiggle", "noodledoodle", "wobblewhack", "zippityjib", "fiddleflap", "bunglebop",
        "skibberdoodle", "flubbernoodle", "quibblequack", "wobblewobble", "gobbleflop", "snickerdoodle", "bumbleflap",
        "quizzlesnort", "noodlewiggle", "skedoodle", "flibberwhack", "bumbersnort", "wobbleflap", "zippitydoodle",
        "gobbledoodle", "blibberbop", "quirkysnicker", "snickerbop", "noodlequack", "wobblefiddle", "dinglewobble",
        "skibberwhack", "flibberflap", "bunglequack", "quibblebop", "gobblewiggle", "zippitysnort", "wobbleflib",
        "flibberjibber", "quizzlewump", "snickerdoodle", "blunderbuss", "gobbledygook", "lollygag", "wobbleflop",
        "noodlebrain", "zippityzap", "quibblesnort", "bumblebeeble", "pumpernickel", "wobblebonk", "snickersnee",
        "dingleberry", "skedaddle", "fiddlesticks", "ballyhoo", "wigglywoo", "gobsmacked", "ziggityzag", "wobblegobble",
        "quibberquack", "noodleoodle", "fluffernutter", "bunglebop", "skibberjank", "whatchamacallit", "wobbleplop",
        "snickerfritz", "blibberblab", "quokkadoodle", "gobbledoodle", "noodleoodle", "doozywhatsit", "skedaddleskoo",
        "flibbertigibbet", "bumbershoot", "wobblewham", "bazinga", "quibbleflap", "gobblersmack", "noodledoodle",
        "wobblefiddle", "zippitydoo", "fiddlefaddle", "bumbleflop", "skedoodle", "wobbleblib", "quizzlenoodle",
        "bunglewobble", "snickerwhack", "gobbledoodle", "flubberwham", "quibblesquawk", "wobblewump", "zippityflap",
        "noodlebop", "skibberflap", "flibberwobble", "bumbersnicker", "wobblebop", "quirkyquack", "gobbledoodle", "dingleflop",
        "snickersnort", "blibberwiggle", "noodledoodle", "wobblewhack", "zippityjib", "fiddleflap", "bunglebop",
        "skibberdoodle", "flubbernoodle", "quibblequack", "wobblewobble", "gobbleflop", "snickerdoodle", "bumbleflap",
        "quizzlesnort", "noodlewiggle", "skedoodle", "flibberwhack", "bumbersnort", "wobbleflap", "zippitydoodle",
        "gobbledoodle", "blibberbop", "quirkysnicker", "snickerbop", "noodlequack", "wobblefiddle", "dinglewobble",
        "skibberwhack", "flibberflap", "bunglequack", "quibblebop", "gobblewiggle", "zippitysnort", "wobbleflib",
        "flibberjibber", "quizzlewump", "snickerdoodle", "blunderbuss", "gobbledygook", "lollygag", "wobbleflop",
        "noodlebrain", "zippityzap", "quibblesnort", "bumblebeeble", "pumpernickel", "wobblebonk", "snickersnee",
        "dingleberry", "skedaddle", "fiddlesticks", "ballyhoo", "wigglywoo", "gobsmacked", "ziggityzag", "wobblegobble",
        "quibberquack", "noodleoodle", "fluffernutter", "bunglebop", "skibberjank", "whatchamacallit", "wobbleplop",
        "snickerfritz", "blibberblab", "quokkadoodle", "gobbledoodle", "noodleoodle", "doozywhatsit", "skedaddleskoo",
        "flibbertigibbet", "bumbershoot", "wobblewham", "bazinga", "quibbleflap", "gobblersmack", "noodledoodle",
        "wobblefiddle", "zippitydoo", "fiddlefaddle", "bumbleflop", "skedoodle", "wobbleblib", "quizzlenoodle",
        "bunglewobble", "snickerwhack", "gobbledoodle", "flubberwham", "quibblesquawk", "wobblewump", "zippityflap",
        "noodlebop", "skibberflap", "flibberwobble", "bumbersnicker", "wobblebop", "quirkyquack", "gobbledoodle", "dingleflop",
        "snickersnort", "blibberwiggle", "noodledoodle", "wobblewhack", "zippityjib", "fiddleflap", "bunglebop",
        "skibberdoodle", "flubbernoodle", "quibblequack", "wobblewobble", "gobbleflop", "snickerdoodle", "bumbleflap",
        "quizzlesnort", "noodlewiggle", "skedoodle", "flibberwhack", "bumbersnort", "wobbleflap", "zippitydoodle",
        "gobbledoodle", "blibberbop", "quirkysnicker", "snickerbop", "noodlequack", "wobblefiddle", "dinglewobble",
        "skibberwhack", "flibberflap", "bunglequack", "quibblebop", "gobblewiggle", "zippitysnort", "wobbleflib",
        "flibberjibber", "quizzlewump", "snickerdoodle", "blunderbuss", "gobbledygook", "lollygag", "wobbleflop",
        "noodlebrain", "zippityzap", "quibblesnort", "bumblebeeble", "pumpernickel", "wobblebonk", "snickersnee",
        "dingleberry", "skedaddle", "fiddlesticks", "ballyhoo", "wigglywoo", "gobsmacked", "ziggityzag", "wobblegobble",
        "quibberquack", "noodleoodle", "fluffernutter", "bunglebop", "skibberjank", "whatchamacallit", "wobbleplop",
        "snickerfritz", "blibberblab", "quokkadoodle", "gobbledoodle", "noodleoodle", "doozywhatsit", "skedaddleskoo",
        "flibbertigibbet", "bumbershoot", "wobblewham", "bazinga", "quibbleflap", "gobblersmack", "noodledoodle",
        "wobblefiddle", "zippitydoo", "fiddlefaddle", "bumbleflop", "skedoodle", "wobbleblib", "quizzlenoodle",
        "bunglewobble", "snickerwhack", "gobbledoodle", "flubberwham", "quibblesquawk", "wobblewump", "zippityflap",
        "noodlebop", "skibberflap", "flibberwobble", "bumbersnicker", "wobblebop", "quirkyquack", "gobbledoodle", "dingleflop",
        "snickersnort", "blibberwiggle", "noodledoodle", "wobblewhack", "zippityjib", "fiddleflap", "bunglebop",
        "skibberdoodle",
    };

    /// <summary>
    /// Generate tunnelIds
    /// </summary>
    /// <returns>string tunnel id</returns>
    public static string GenerateTunnelId()
    {
        var random = new Random();
        var sb = new StringBuilder();
        for (int i = 0; i < 2; i++)
        {
            if (i!=0)
            {
                sb.Append("-");
            }

            sb.Append(words[random.Next(words.Length)]);
        }
        return sb.ToString();
    }
}
