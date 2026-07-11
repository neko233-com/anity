using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class WebGLVideo
    {
        private string _url = string.Empty;
        private bool _playing;
        private bool _paused;
        private double _time;
        private double _duration;
        private float _volume = 1f;
        private bool _loop;
        private bool _prepared;
        private float _playbackSpeed = 1f;

        public string url
        {
            get => _url;
            set
            {
                _url = value ?? string.Empty;
                _prepared = false;
                _time = 0;
            }
        }

        public bool playing => _playing && !_paused;
        public bool paused => _paused;
        public bool isPlaying => _playing && !_paused;
        public bool isPaused => _paused;
        public bool isPrepared => _prepared;

        public double time
        {
            get => _time;
            set => _time = Math.Max(0, value);
        }

        public double duration => _duration;
        public double length => _duration;

        public float volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public bool loop
        {
            get => _loop;
            set => _loop = value;
        }

        public float playbackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = value;
        }

        public event Action<WebGLVideo>? prepareCompleted;
        public event Action<WebGLVideo>? started;
        public event Action<WebGLVideo>? loopPointReached;
        public event Action<WebGLVideo, string>? errorReceived;

        public WebGLVideo()
        {
        }

        public WebGLVideo(string url)
        {
            _url = url ?? string.Empty;
        }

        public void Prepare()
        {
            _prepared = true;
            _duration = EstimateDurationFromUrl(_url);
            prepareCompleted?.Invoke(this);
        }

        public void Play()
        {
            if (!_prepared)
            {
                Prepare();
            }
            _playing = true;
            _paused = false;
            started?.Invoke(this);
        }

        public void Pause()
        {
            _paused = true;
        }

        public void UnPause()
        {
            if (_playing)
            {
                _paused = false;
            }
        }

        public void Stop()
        {
            _playing = false;
            _paused = false;
            _time = 0;
        }

        internal void UpdateTime(float deltaTime)
        {
            if (!_playing || _paused) return;

            _time += deltaTime * _playbackSpeed;

            if (_duration > 0 && _time >= _duration)
            {
                loopPointReached?.Invoke(this);
                if (_loop)
                {
                    _time = 0;
                }
                else
                {
                    _playing = false;
                }
            }
        }

        private double EstimateDurationFromUrl(string url)
        {
            return 0;
        }
    }

    public static class WebGLApplication
    {
        private static readonly Dictionary<string, WebGLVideo> _activeVideos = new();
        private static WebGLVideo? _currentVideo;

        public static bool isWebGL => Application.isWebGL;

        public static WebGLVideo PlayVideo(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            if (!_activeVideos.TryGetValue(url, out var video))
            {
                video = new WebGLVideo(url);
                _activeVideos[url] = video;
            }

            video.Play();
            _currentVideo = video;
            return video;
        }

        public static void PauseVideo(string url)
        {
            if (_activeVideos.TryGetValue(url, out var video))
            {
                video.Pause();
            }
        }

        public static void StopVideo(string url)
        {
            if (_activeVideos.TryGetValue(url, out var video))
            {
                video.Stop();
                _activeVideos.Remove(url);
                if (_currentVideo == video)
                {
                    _currentVideo = null;
                }
            }
        }

        public static void PauseCurrentVideo()
        {
            _currentVideo?.Pause();
        }

        public static void StopCurrentVideo()
        {
            if (_currentVideo != null)
            {
                StopVideo(_currentVideo.url);
            }
        }

        public static WebGLVideo? GetVideo(string url)
        {
            _activeVideos.TryGetValue(url, out var video);
            return video;
        }

        public static WebGLVideo? GetCurrentVideo() => _currentVideo;

        internal static void UpdateVideos(float deltaTime)
        {
            foreach (var video in _activeVideos.Values)
            {
                video.UpdateTime(deltaTime);
            }
        }
    }
}
