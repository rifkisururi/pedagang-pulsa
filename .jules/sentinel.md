## 2024-05-30 - Insecure Random Number Generation

**Vulnerability:** Use of `System.Random` for generating referral codes and transaction reference IDs, and not using `RandomNumberGenerator`. `System.Random` is predictable and not cryptographically secure.
**Learning:** `System.Random` was used in `AuthService.cs`, `AuthController.cs`, and `TransactionController.cs` for generating unique identifiers. This could lead to predictable codes, allowing attackers to guess referral codes or transaction reference IDs.
**Prevention:** Always use `System.Security.Cryptography.RandomNumberGenerator` for generating sensitive random values like referral codes, tokens, or transaction IDs.
