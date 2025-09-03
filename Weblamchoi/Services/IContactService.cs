using weblamchoi.Models;

namespace weblamchoi.Services
{
    public interface IContactService
    {
        Task<List<Contact>> GetAllAsync();
        Task<Contact> GetByIdAsync(int id);
        Task AddAsync(Contact contact);
        Task DeleteAsync(int id);
    }
}
