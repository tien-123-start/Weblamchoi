using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using weblamchoi.Services;

public class ContactService : IContactService
{
    private readonly DienLanhDbContext _context;
 
    public ContactService(DienLanhDbContext context)
    {
        _context = context;
    }

    public async Task<List<Contact>> GetAllAsync()
    {
        return await _context.Contacts
            .OrderByDescending(c => c.SubmittedAt)
            .ToListAsync();
    }

    public async Task<Contact> GetByIdAsync(int id)
    {
        return await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Contact contact)
    {
        contact.SubmittedAt = DateTime.Now;
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var contact = await _context.Contacts.FindAsync(id);
        if (contact != null)
        {
            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync();
        }
    }
}
