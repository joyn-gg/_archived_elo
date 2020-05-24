using System;
using System.Collections.Generic;
using System.Text;

namespace ELO.Services.Premium
{
    public class DeletedPremiumUser
    {
        public ulong UserId { get; set; }

        public ulong EntitledRoleId { get; set; }

        public int EntitledRegistrationCount { get; set; }

        public DateTime LastSuccessfulKnownPayment { get; set; }
    }
}