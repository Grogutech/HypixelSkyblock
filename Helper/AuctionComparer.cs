using System.Collections.Generic;

namespace hypixel
{
    public class AuctionComparer : IEqualityComparer<SaveAuction>
    {
        public bool Equals(SaveAuction x, SaveAuction y)
        {
            return x.Uuid == y.Uuid;
        }

        public int GetHashCode(SaveAuction obj)
        {
            return obj.GetHashCode();
        }
    }
}