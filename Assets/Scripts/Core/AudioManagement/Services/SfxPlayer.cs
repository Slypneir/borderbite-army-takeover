using System;
using Core.AudioManagement.Configuration;
using Core.AudioManagement.Enums;
using Core.AudioManagement.Interfaces;
using UnityEngine;

namespace Core.AudioManagement.Services
{
    /// <summary>One 2D audio source for all the one-shots; the volume of each comes from <see cref="SoundVolumes"/>.</summary>
    public sealed class SfxPlayer : ISfxPlayer, IDisposable
    {
        private readonly SoundVolumes _volumes;
        private AudioSource _source;

        public SfxPlayer(SoundVolumes volumes)
        {
            _volumes = volumes;
        }

        public void Play(Sfx sound)
        {
            var (clip, volume) = _volumes.Of(sound);
            if (clip == null || volume <= 0f)
                return;

            Source().PlayOneShot(clip, Mathf.Min(volume, 1f));
        }

        public void Dispose()
        {
            if (_source != null)
                UnityEngine.Object.Destroy(_source.gameObject);
        }

        private AudioSource Source()
        {
            if (_source != null)
                return _source;

            var go = new GameObject("SfxPlayer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            return _source;
        }
    }
}
