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
            CreateMap<Symptom, SymptomModel>().ReverseMap();
            CreateMap<RegisterModel, User>().ForMember(u => u.PasswordHash, opt => opt.Ignore());
        }
    }
}
