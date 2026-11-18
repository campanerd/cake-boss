namespace CakeBoss.Backend.Shared;

/// <summary>
/// Violação de uma regra de negócio. A mensagem é escrita em português para poder ser exibida ao usuário.
/// </summary>
public class DomainException(string message) : Exception(message);
