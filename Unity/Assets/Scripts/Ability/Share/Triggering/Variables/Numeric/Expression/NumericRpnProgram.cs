using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    public sealed class NumericRpnProgram
    {
        public NumericRpnProgram(string expr, NumericRpnToken[] tokens)
        {
            Expr = expr ?? throw new ArgumentNullException(nameof(expr));
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        public string Expr { get; }
        public NumericRpnToken[] Tokens { get; }
    }
}
