using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
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
