using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_Test
{
    internal class TestService : PeriodicTaskBase
    {
        protected override string TaskName => "TestService";
        protected override int TaskOrder => 1;

        protected override bool IsEnabled => true;
        protected override bool IsDisabled => false;

        public TestService()
        {
        }

        protected override bool ProcessItem()
        {
            throw new NotImplementedException();
        }
    }
}
