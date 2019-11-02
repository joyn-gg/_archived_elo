using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ELO.CompetitionModels.Legacy
{
    public class Token
    {
        [Key]
        public string Key { get; set; }
        public int Days { get; set; }
    }
}
