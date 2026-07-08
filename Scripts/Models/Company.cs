using System.Collections.Generic;
namespace ColdWarWargame.Models
{
    public class Company
    {
        public string CompanyId { get; set; }
        public string Name { get; set; }
        public List<Platoon> Platoons { get; set; } = new List<Platoon>();
    }
}