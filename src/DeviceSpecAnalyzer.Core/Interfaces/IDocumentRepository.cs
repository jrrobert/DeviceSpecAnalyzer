using DeviceSpecAnalyzer.Core.Models;

namespace DeviceSpecAnalyzer.Core.Interfaces;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(int id);
    Task<Document?> GetByFileNameAsync(string fileName);
    Task<Document?> GetByHashAsync(string hash);
    Task<IEnumerable<Document>> GetAllAsync();
    Task<IEnumerable<Document>> GetByProtocolAsync(string protocol);
    Task<IEnumerable<Document>> GetByManufacturerAsync(string manufacturer);
    Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status);
    Task<IEnumerable<Document>> SearchAsync(string searchTerm);
    Task<Document> AddAsync(Document document);
    Task UpdateAsync(Document document);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string fileName);
    Task<bool> HashExistsAsync(string hash);
    Task<IEnumerable<Document>> GetRecentAsync(int count);
    Task<int> GetCountByStatusAsync(DocumentStatus status);
}