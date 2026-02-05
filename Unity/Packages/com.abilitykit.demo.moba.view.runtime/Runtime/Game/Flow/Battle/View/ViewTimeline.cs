using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed class ViewTimeline
    {
        private readonly List<IFrameSeekableView> _views = new List<IFrameSeekableView>(32);

        public void Register(IFrameSeekableView view)
        {
            if (view == null) return;
            if (_views.Contains(view)) return;
            _views.Add(view);
        }

        public void Unregister(IFrameSeekableView view)
        {
            if (view == null) return;
            _views.Remove(view);
        }

        public void Clear()
        {
            _views.Clear();
        }

        public void SeekAll(int frameIndex, float secondsPerFrame)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i]?.SeekToFrame(frameIndex, secondsPerFrame);
            }
        }
    }
}
