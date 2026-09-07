
namespace AbilityKit.HFSM
{
	public interface ITransitionListener
	{
		void BeforeTransition();
		void AfterTransition();
	}
}
