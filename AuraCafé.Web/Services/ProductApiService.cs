
using System.Net;
using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class ProductApiService
{
	private readonly HttpClient _httpClient;

	public ProductApiService(HttpClient httpClient)
	{
		_httpClient = httpClient;
	}

	// GET: api/products
	public async Task<List<ProductDto>> GetAllAsync()
	{
		return await _httpClient
			.GetFromJsonAsync<List<ProductDto>>("api/products")
			?? new List<ProductDto>();
	}

	// GET: api/products/{id}
	public async Task<ProductDto?> GetByIdAsync(int id)
	{
		using var response = await _httpClient.GetAsync(
			$"api/products/{id}");

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();

		return await response.Content
			.ReadFromJsonAsync<ProductDto>();
	}

	// GET: api/categories
	public async Task<List<CategoryDto>> GetCategoriesAsync()
	{
		return await _httpClient
			.GetFromJsonAsync<List<CategoryDto>>("api/categories")
			?? new List<CategoryDto>();
	}

	// POST: api/products
	public async Task CreateAsync(ProductFormDto product)
	{
		var request = new
		{
			product.Name,
			product.Description,
			product.PriceCents,
			product.CategoryId
		};

		using var response = await _httpClient.PostAsJsonAsync(
			"api/products", request);

		await EnsureSuccessAsync(response);
	}

	// PUT: api/products/{id}
	public async Task UpdateAsync(int id, ProductFormDto product)
	{
		using var response = await _httpClient.PutAsJsonAsync(
			$"api/products/{id}", product);

		await EnsureSuccessAsync(response);
	}

	// DELETE: api/products/{id}
	public async Task DeleteAsync(int id)
	{
		using var response = await _httpClient.DeleteAsync(
			$"api/products/{id}");

		await EnsureSuccessAsync(response);
	}

	private static async Task EnsureSuccessAsync(
		HttpResponseMessage response)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var body = await response.Content.ReadAsStringAsync();

		throw new HttpRequestException(
			$"API returned {(int)response.StatusCode}: {body}");
	}
}
