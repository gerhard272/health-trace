using AutoMapper;
using HealthTrace.BLL.Models;
using HealthTrace.DAL.Entities;

namespace HealthTrace.PL.API.Configurations
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, UserModel>().ReverseMap();
            CreateMap<Patient, PatientModel>().ReverseMap();
            CreateMap<Doctor, DoctorModel>().ReverseMap();
            CreateMap<Appointment, AppointmentModel>().ReverseMap();
            CreateMap<Prescription, PrescriptionModel>().ReverseMap();
            CreateMap<MedicalRecord, MedicalRecordModel>().ReverseMap();
        }
    }
}
