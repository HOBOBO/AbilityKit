namespace AbilityKit.Triggering.Variables.Numeric
{
    public readonly struct NumericVarRef
    {
        public readonly string DomainId;
        public readonly string Key;

        public NumericVarRef(string domainId, string key)
        {
            DomainId = domainId;
            Key = key;
        }
    }
}
