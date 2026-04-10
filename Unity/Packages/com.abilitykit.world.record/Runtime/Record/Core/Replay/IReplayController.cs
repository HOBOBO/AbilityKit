using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    public interface IReplayController
    {
        bool IsPlaying { get; }

        void Play();
        void Pause();

        void SeekToStart();
        void SeekToFrame(FrameIndex frame);

        void Tick(float deltaTime);
    }
}
