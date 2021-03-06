using System.Collections.Generic;
using Newtonsoft.Json;

namespace Halite.Tests
{
    internal class DummyLinksWithPrivateConstructor : HalLinks
    {
        [JsonConstructor]
        private DummyLinksWithPrivateConstructor(SelfLink self, ThisLink @this, ThatLink that, IReadOnlyList<HalLink> those) : base(self)
        {
            This = @this;
            That = that;
            Those = those;
        }

        [HalRelation("this")]
        public ThisLink This { get; }

        [HalRelation("that")]
        public ThatLink That { get; }

        [HalRelation("those")]
        public IReadOnlyList<HalLink> Those { get; }
    }
}