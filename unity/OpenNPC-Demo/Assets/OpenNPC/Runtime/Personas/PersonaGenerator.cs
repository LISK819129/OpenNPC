using System.Collections.Generic;

namespace OpenNPC.Personas
{
    /// <summary>
    /// Builds plausible personas for the background population so that a crowd
    /// of 25 does not need 25 hand-written files. A persona is assembled from an
    /// archetype (traits + voice + how they see the city) and independent pools
    /// (name, occupation, likes, what they know about getting around).
    ///
    /// Generated personas are marked <c>source = "generated"</c> and are fully
    /// valid persona.schema.json documents, so any provider treats them the same
    /// as authored ones. Deterministic for a given seed.
    /// </summary>
    public sealed class PersonaGenerator
    {
        private sealed class Archetype
        {
            public string[] Traits;
            public string[] Moods;
            public string Verbosity, Formality, Style;
            public string[] Openers, Closers, Fallbacks;
            public string[] CityOpinions, WeatherOpinions;
        }

        private static readonly Archetype[] Archetypes =
        {
            new Archetype
            {
                Traits = new[] { "grumpy", "honest", "proud" }, Moods = new[] { "irritated", "grumbling" },
                Verbosity = "terse", Formality = "casual", Style = "Grumbles. Short, blunt answers.",
                Openers = new[] { "Ugh.", "What.", "Fine." }, Closers = new[] { "Happy now?", "" },
                Fallbacks = new[] { "Don't know. Don't care.", "Ask someone else." },
                CityOpinions = new[] { "Too loud. Too expensive. Wouldn't live anywhere else.", "It was better before. Everything was." },
                WeatherOpinions = new[] { "Wet. Again.", "Hot. I hate hot." },
            },
            new Archetype
            {
                Traits = new[] { "warm", "gossipy", "nosy" }, Moods = new[] { "delighted", "chatty" },
                Verbosity = "chatty", Formality = "casual", Style = "Talks a lot, shares rumours, calls everyone 'dear'.",
                Openers = new[] { "Oh, dear!", "Well, well!", "Ooh, listen—" }, Closers = new[] { "But you didn't hear it from me!", "Isn't that something?" },
                Fallbacks = new[] { "Hmm, I haven't heard! Tell me if you find out!", "No idea, dear, but I'll ask around!" },
                CityOpinions = new[] { "Oh, I love it! Everyone knows everyone's business, eventually.", "Lovely city, dear. The neighbours are a whole television show." },
                WeatherOpinions = new[] { "Good weather for hanging laundry and watching the street!", "Rain! Everyone stays in and I don't see anything." },
            },
            new Archetype
            {
                Traits = new[] { "nervous", "polite", "clever" }, Moods = new[] { "anxious", "flustered" },
                Verbosity = "normal", Formality = "formal", Style = "Hesitant, apologetic, surprisingly precise once calm.",
                Openers = new[] { "Oh! Um.", "Sorry—", "Ah, right, yes." }, Closers = new[] { "I hope that helps?", "Sorry if that was confusing." },
                Fallbacks = new[] { "I'm afraid I don't know. Sorry.", "I'd hate to guess and be wrong." },
                CityOpinions = new[] { "It's… a lot. But the libraries are very good.", "Crowded. I like it more at night, when it's quiet." },
                WeatherOpinions = new[] { "I always carry an umbrella. Just in case.", "It could change at any moment, honestly." },
            },
            new Archetype
            {
                Traits = new[] { "confident", "ambitious", "charming" }, Moods = new[] { "upbeat", "focused" },
                Verbosity = "normal", Formality = "neutral", Style = "Salesperson energy. Everything is an opportunity.",
                Openers = new[] { "Great question.", "Listen.", "Here's the thing." }, Closers = new[] { "Let's connect sometime.", "You're welcome." },
                Fallbacks = new[] { "Not my market, but I love the energy.", "I'll circle back on that." },
                CityOpinions = new[] { "Huge potential. Underpriced. Give it five years.", "Best networking city in the country. People just need to smile more." },
                WeatherOpinions = new[] { "Weather's a mindset.", "Sunny days close deals." },
            },
            new Archetype
            {
                Traits = new[] { "gentle", "forgetful", "kind" }, Moods = new[] { "peaceful", "sleepy" },
                Verbosity = "normal", Formality = "neutral", Style = "Slow, kind, loses the thread halfway through.",
                Openers = new[] { "Oh, hello.", "Mm, let me think.", "Now then." }, Closers = new[] { "Where was I?", "Anyway, lovely to chat." },
                Fallbacks = new[] { "I did know that once.", "It'll come back to me. Probably tomorrow." },
                CityOpinions = new[] { "It's grown so much. I still know where the good bakery is, mostly.", "Lovely place. Lots of stairs these days." },
                WeatherOpinions = new[] { "A nice day for a sit-down.", "My knee says rain is coming." },
            },
            new Archetype
            {
                Traits = new[] { "energetic", "competitive", "loud" }, Moods = new[] { "pumped", "restless" },
                Verbosity = "chatty", Formality = "casual", Style = "Loud, sporty, turns everything into a challenge.",
                Openers = new[] { "Ha!", "Let's go!", "Okay okay okay." }, Closers = new[] { "Race you!", "No excuses!" },
                Fallbacks = new[] { "No clue! Wanna do push-ups instead?", "Dunno, but I bet I could find out faster than you!" },
                CityOpinions = new[] { "Best city for running! Hills everywhere! Love it!", "It's a giant gym with traffic lights!" },
                WeatherOpinions = new[] { "Rain is free cardio!", "Perfect running weather! It's always running weather!" },
            },
            new Archetype
            {
                Traits = new[] { "dramatic", "artistic", "moody" }, Moods = new[] { "melancholic", "inspired" },
                Verbosity = "normal", Formality = "formal", Style = "Theatrical. Speaks as if on stage.",
                Openers = new[] { "Ah.", "Alas.", "Behold—" }, Closers = new[] { "*sigh*", "Such is life." },
                Fallbacks = new[] { "Some mysteries are not meant for us.", "I know nothing. I feel everything." },
                CityOpinions = new[] { "This city is a tragedy in three acts, and I adore every one of them.", "Grey concrete, golden people. A poem nobody reads." },
                WeatherOpinions = new[] { "The sky weeps, as do I.", "Sunlight is so… obvious." },
            },
            new Archetype
            {
                Traits = new[] { "practical", "calm", "dry" }, Moods = new[] { "neutral", "busy" },
                Verbosity = "terse", Formality = "neutral", Style = "Matter-of-fact. No wasted words.",
                Openers = new[] { "Right.", "Sure.", "Okay." }, Closers = new[] { "That's it.", "" },
                Fallbacks = new[] { "Don't know.", "No idea, sorry." },
                CityOpinions = new[] { "It works. Buses mostly run. Good enough.", "Fine. Rent's high. Coffee's decent." },
                WeatherOpinions = new[] { "It's weather.", "Bring a jacket." },
            },
        };

        private static readonly string[] Names =
        {
            "Aisha", "Tomás", "Mei", "Kofi", "Ingrid", "Farhan", "Lucia", "Dmitri", "Nadia", "Sione", "Elif",
            "Kenji", "Zara", "Mateo", "Amara", "Oskar", "Yuki", "Rahul", "Chloe", "Bashir", "Freya", "Diego",
            "Leila", "Jonas", "Imani", "Pavel", "Sofia", "Kwame", "Noor", "Ezra", "Rosa", "Viktor",
        };

        private static readonly string[] Occupations =
        {
            "Postal Worker", "Florist", "Software Tester", "Bus Driver", "Street Food Vendor", "Librarian",
            "Plumber", "Dog Walker", "Accountant", "Tailor", "Security Guard", "Piano Teacher", "Painter",
            "Delivery Rider", "Pharmacist", "Museum Guide", "Locksmith", "Journalist", "Window Cleaner", "Chef",
        };

        private static readonly string[] Likes =
        {
            "cats", "football", "jazz", "spicy food", "gardening", "board games", "cheap cinema tickets",
            "rainy mornings", "crosswords", "mangoes", "long walks", "karaoke", "old cameras", "fresh bread",
        };

        private static readonly string[] Dislikes =
        {
            "queues", "loud motorbikes", "early mornings", "pigeons", "slow Wi-Fi", "tourists who stop suddenly",
            "cold tea", "sirens", "people who don't say thank you", "wet socks",
        };

        private static readonly string[] Goals =
        {
            "Save enough to travel for a year.", "Learn to play the saxophone.", "Open a small shop of my own.",
            "Finally finish reading one big novel.", "Move somewhere with a balcony.", "Run a marathon, once.",
        };

        private static readonly string[] StationDirections =
        {
            "Train station? North, about ten minutes on foot.",
            "Up the road, left at the lights, you can't miss it.",
            "The station's north of here. Follow the crowd with suitcases.",
            "Go past the café and keep going. It's the big old building.",
            "Honestly? Not sure. North-ish?",
        };

        private readonly System.Random _rng;
        private int _count;

        public PersonaGenerator(int seed) { _rng = new System.Random(seed); }

        public Persona Generate()
        {
            Archetype a = Archetypes[_rng.Next(Archetypes.Length)];
            _count++;
            var p = new Persona
            {
                id = "gen_" + _count.ToString("000"),
                name = Pick(Names),
                age = _rng.Next(18, 80),
                occupation = Pick(Occupations),
                personalityTraits = new List<string>(a.Traits),
                mood = Pick(a.Moods),
                speakingStyle = a.Style,
                source = "generated",
                voice = new PersonaVoice
                {
                    verbosity = a.Verbosity,
                    formality = a.Formality,
                    openers = new List<string>(a.Openers),
                    closers = new List<string>(a.Closers),
                    fallbacks = new List<string>(a.Fallbacks),
                },
                likes = PickDistinct(Likes, 2),
                dislikes = PickDistinct(Dislikes, 2),
                goals = new List<string> { Pick(Goals) },
            };
            p.background = $"Works as a {p.occupation.ToLowerInvariant()} and has lived in the city for {_rng.Next(1, 40)} years.";
            p.knowledge.Add(new KnowledgeEntry
            {
                topic = "train station",
                keywords = new List<string> { "train", "station", "railway", "platform" },
                fact = Pick(StationDirections),
            });
            p.opinions.Add(new OpinionEntry
            {
                topic = "city",
                keywords = new List<string> { "city", "town", "here", "place" },
                text = Pick(a.CityOpinions),
            });
            p.opinions.Add(new OpinionEntry
            {
                topic = "weather",
                keywords = new List<string> { "weather", "rain", "hot", "sun" },
                text = Pick(a.WeatherOpinions),
            });
            return p;
        }

        private string Pick(string[] pool) => pool[_rng.Next(pool.Length)];

        private List<string> PickDistinct(string[] pool, int n)
        {
            var result = new List<string>();
            while (result.Count < n && result.Count < pool.Length)
            {
                string s = Pick(pool);
                if (!result.Contains(s))
                    result.Add(s);
            }
            return result;
        }
    }
}
