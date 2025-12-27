//------------------------------------------------------------
//        File:  RandomProbability.cs
//       Brief:  RandomProbability
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-16
//============================================================

using System;

namespace BTCore.Runtime.Conditions
{
    public class RandomProbability : Condition
    {
        public SharedValue<int> Probability { get; set; } = new();

        private Random _random;
        
        protected override void OnStart() {
            base.OnStart();
            _random = new Random();
        }

        protected override void OnStop() {
            
        }

        protected override bool Validate() {
            var randomValue = _random.Next(0, 100);
            return randomValue < Probability.Value;
        }
    }
}