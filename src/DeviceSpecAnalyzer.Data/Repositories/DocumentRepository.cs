using Microsoft.EntityFrameworkCore;
using DeviceSpecAnalyzer.Core.Interfaces;
using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Data.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _context;

    public DocumentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(int id)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .Include(d => d.Sections)
            .Include(d => d.DeviceDrivers)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document?> GetByFileNameAsync(string fileName)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .FirstOrDefaultAsync(d => d.FileName == fileName);
    }

    public async Task<Document?> GetByHashAsync(string hash)
    {
        return await _context.Documents
            .FirstOrDefaultAsync(d => d.FileHash == hash);
    }

    public async Task<IEnumerable<Document>> GetAllAsync()
    {
        return await _context.Documents
            .Include(d => d.Content)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Document>> GetByProtocolAsync(string protocol)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .Where(d => d.Protocol == protocol)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Document>> GetByManufacturerAsync(string manufacturer)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .Where(d => d.Manufacturer == manufacturer)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Document>> SearchAsync(string searchTerm)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .Where(d => 
                d.FileName.Contains(searchTerm) ||
                d.Manufacturer!.Contains(searchTerm) ||
                d.DeviceName!.Contains(searchTerm) ||
                d.Protocol!.Contains(searchTerm) ||
                (d.Content != null && d.Content.Keywords!.Contains(searchTerm)))
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<Document> AddAsync(Document document)
    {
        document.UploadedAt = DateTime.UtcNow;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task UpdateAsync(Document document)
    {
        _context.Entry(document).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document != null)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string fileName)
    {
        return await _context.Documents.AnyAsync(d => d.FileName == fileName);
    }

    public async Task<bool> HashExistsAsync(string hash)
    {
        return await _context.Documents.AnyAsync(d => d.FileHash == hash);
    }

    public async Task<IEnumerable<Document>> GetRecentAsync(int count)
    {
        return await _context.Documents
            .Include(d => d.Content)
            .OrderByDescending(d => d.UploadedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetCountByStatusAsync(DocumentStatus status)
    {
        return await _context.Documents.CountAsync(d => d.Status == status);
    }
}