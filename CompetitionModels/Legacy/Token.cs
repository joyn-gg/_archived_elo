using System.ComponentModel.DataAnnotations;

namespace ELO.CompetitionModels.Legacy
{
    public class Token
    {
        [Key]
        public string Key { get; set; }
        public int Days { get; set; }
    }
}
