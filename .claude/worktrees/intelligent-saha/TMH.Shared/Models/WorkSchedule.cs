using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMH.Shared.Models
{
    public class WorkSchedule
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int MaxPatients { get; set; } = 10;
        public int CurrentPatients { get; set; } = 0;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }

}
