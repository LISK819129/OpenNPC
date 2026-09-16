using System.Collections.Generic;
using UnityEngine;

namespace OpenNPC.Personas
{
    /// <summary>
    /// Loads authored personas from a JSON bundle and tops the population up
    /// with generated ones. Handing out personas is sequential and never
    /// repeats an id, so no two NPCs in a scene share a mind.
    /// </summary>
    public sealed class PersonaLibrary
    {
        private readonly List<Persona> _authored = new List<Persona>();
        private readonly PersonaGenerator _generator;
        private readonly HashSet<string> _issued = new HashSet<string>();
        private int _nextAuthored;

        public IReadOnlyList<Persona> Authored => _authored;

        public PersonaLibrary(string bundleJson, int generatorSeed)
        {
            _generator = new PersonaGenerator(generatorSeed);
            if (string.IsNullOrWhiteSpace(bundleJson))
            {
                Debug.LogWarning("[OpenNPC] Persona bundle is empty; every NPC will be generated.");
                return;
            }
            PersonaCollection collection = JsonUtility.FromJson<PersonaCollection>(bundleJson);
            if (collection?.personas == null)
                return;
            foreach (Persona p in collection.personas)
            {
                if (string.IsNullOrEmpty(p.id) || string.IsNullOrEmpty(p.name))
                {
                    Debug.LogWarning("[OpenNPC] Skipping persona without id or name.");
                    continue;
                }
                _authored.Add(p);
            }
        }

        /// <summary>Authored personas first, in bundle order; generated ones after that.</summary>
        public Persona Next()
        {
            while (_nextAuthored < _authored.Count)
            {
                Persona p = _authored[_nextAuthored++];
                if (_issued.Add(p.id))
                    return p;
            }
            Persona generated;
            do generated = _generator.Generate();
            while (!_issued.Add(generated.id));
            return generated;
        }
    }
}
