using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    public enum NumericRpnTokenKind
    {
        Number = 0,
        Var = 1,
        Add = 2,
        Sub = 3,
        Mul = 4,
        Div = 5,
        Func = 6
    }

    public readonly struct NumericRpnToken
    {
        private NumericRpnToken(NumericRpnTokenKind kind, double number, string domainId, string key, string funcName, int funcArgCount)
        {
            Kind = kind;
            Number = number;
            DomainId = domainId;
            Key = key;
            FuncName = funcName;
            FuncArgCount = funcArgCount;
        }

        public NumericRpnTokenKind Kind { get; }
        public double Number { get; }
        public string DomainId { get; }
        public string Key { get; }
        public string FuncName { get; }
        public int FuncArgCount { get; }

        public static NumericRpnToken NumberToken(double value) => new NumericRpnToken(NumericRpnTokenKind.Number, value, null, null, null, 0);
        public static NumericRpnToken VarToken(string domainId, string key) => new NumericRpnToken(NumericRpnTokenKind.Var, 0d, domainId, key, null, 0);
        public static NumericRpnToken AddToken() => new NumericRpnToken(NumericRpnTokenKind.Add, 0d, null, null, null, 0);
        public static NumericRpnToken SubToken() => new NumericRpnToken(NumericRpnTokenKind.Sub, 0d, null, null, null, 0);
        public static NumericRpnToken MulToken() => new NumericRpnToken(NumericRpnTokenKind.Mul, 0d, null, null, null, 0);
        public static NumericRpnToken DivToken() => new NumericRpnToken(NumericRpnTokenKind.Div, 0d, null, null, null, 0);
        public static NumericRpnToken FuncToken(string name, int argCount) => new NumericRpnToken(NumericRpnTokenKind.Func, 0d, null, null, name, argCount);
    }
}
