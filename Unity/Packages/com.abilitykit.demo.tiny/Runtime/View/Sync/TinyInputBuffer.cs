namespace AbilityKit.Demo.Tiny.View
{
    /// <summary>Keeps the latest movement and one pending attack while a send is in flight.</summary>
    internal sealed class TinyInputBuffer
    {
        private sbyte _moveX;
        private sbyte _moveY;
        private bool _attack;

        public void Capture(TinyInput input)
        {
            _moveX = input.MoveX;
            _moveY = input.MoveY;
            _attack |= input.Attack;
        }

        public bool TryTake(out TinyInput input)
        {
            if (_moveX == 0 && _moveY == 0 && !_attack)
            {
                input = default;
                return false;
            }
            input = new TinyInput(_moveX, _moveY, _attack);
            _attack = false;
            return true;
        }

        public void Clear()
        {
            _moveX = 0;
            _moveY = 0;
            _attack = false;
        }
    }
}
