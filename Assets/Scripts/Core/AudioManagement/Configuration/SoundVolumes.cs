using System;
using System.Collections.Generic;
using Core.AudioManagement.Enums;
using UnityEngine;

namespace Core.AudioManagement.Configuration
{
    /// <summary>
    /// The table of all the sounds: a row per sound with its clip and its volume in percent, 100 by default. No sound of the game
    /// plays past it. The owner edits the percents in the Inspector, also in Play Mode: the next sound takes the new value.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundVolumes", menuName = "Borderbite/Audio/Sound Volumes")]
    public sealed class SoundVolumes : ScriptableObject
    {
        [Serializable]
        public sealed class Row
        {
            public Sfx sound;
            public AudioClip clip;
            [Range(0, 200)] public int percent = 100;
        }

        [Tooltip("Громкость каждого звука в процентах: 100 — как в файле, 0 — молчит, выше 100 — громче, но не громче полной громкости.")]
        [SerializeField] private List<Row> sounds = new List<Row>();

        /// <summary>The clip of a sound and its factor of volume; no clip when the table has no row.</summary>
        public (AudioClip Clip, float Volume) Of(Sfx sound)
        {
            foreach (var row in sounds)
            {
                if (row.sound == sound)
                    return (row.clip, row.percent / 100f);
            }

            return (null, 0f);
        }

#if UNITY_EDITOR
        /// <summary>Adds a row for a sound the table lacks, at 100 %; a row the owner tuned stays as it is.</summary>
        public bool EnsureRow(Sfx sound, AudioClip clip)
        {
            foreach (var row in sounds)
            {
                if (row.sound != sound)
                    continue;

                if (row.clip != null)
                    return false;

                row.clip = clip;
                return true;
            }

            sounds.Add(new Row { sound = sound, clip = clip });
            return true;
        }
#endif
    }
}
