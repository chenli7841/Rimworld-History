namespace HistoricalRimWorld.Politics
{
    public static class HistoricalPoliticalRelationRules
    {
        public static bool IsDirected(HistoricalPoliticalRelationType relationType)
        {
            switch (relationType)
            {
                case HistoricalPoliticalRelationType.NominalSuzerainty:
                case HistoricalPoliticalRelationType.EffectiveSubordination:
                case HistoricalPoliticalRelationType.Tributary:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSymmetricForLookup(HistoricalPoliticalRelationType relationType)
        {
            return relationType == HistoricalPoliticalRelationType.Alliance;
        }

        public static bool HasSameIdentity(
            string leftSource,
            string leftTarget,
            HistoricalPoliticalRelationType leftType,
            string rightSource,
            string rightTarget,
            HistoricalPoliticalRelationType rightType)
        {
            if (leftType != rightType) return false;
            if (IsSymmetricForLookup(leftType))
            {
                return (leftSource == rightSource && leftTarget == rightTarget)
                    || (leftSource == rightTarget && leftTarget == rightSource);
            }

            return leftSource == rightSource && leftTarget == rightTarget;
        }
    }
}
