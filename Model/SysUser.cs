using System;
using System.Collections.Generic;

namespace EFCoreTest
{
    public partial class SysUser
    {
        public string Id { get; set; } = null!;
        public string? Name { get; set; }
        public string? TestField { get; set; }
        public string? TestField2 { get; set; }
    }
}
