using BCrypt.Net;
using HealthTrace.BLL.Security;

namespace HealthTrace.Test.BLL.Security
{
    /// <summary>
    /// Test di PasswordHasher, l'unica implementazione di IPasswordHasher: il punto in cui
    /// l'applicazione produce e verifica i segreti degli utenti.
    /// Nessun mock, perche' la classe non ha dipendenze e delega a BCrypt: e' anche l'unico punto
    /// della suite in cui BCrypt viene davvero eseguito, dato che nei test di UserService l'hasher
    /// e' mockato. I valori usati sono inventati e non plausibili come credenziali reali.
    /// Attenzione al costo: work factor 11, circa 100 ms per operazione. Non va ridotto per
    /// velocizzare la suite, altrimenti i test non proteggerebbero l'algoritmo di produzione.
    /// Attenzione all'ordine degli argomenti: VerifyPassword riceve l'hash prima e la password in
    /// chiaro dopo, al contrario di BCrypt.Verify(text, hash).
    /// </summary>
    public class PasswordHasherTests
    {
        private const string TestPassword = "Password-di-test-1!";

        private const string BcryptPrefix = "$2a$";

        // Il fattore di lavoro e' scritto qui per esteso e non letto da BCrypt.DefaultRounds: un
        // downgrade dell'algoritmo deve rompere la suite, non passare silenziosamente.
        private const int BcryptWorkFactor = 11;
        private const int BcryptHashLength = 60;

        // 72 byte e' il massimo che BCrypt considera: coincide con il PasswordMaxLength del
        // validator di registrazione, quindi una password al confine arriva al hasher per intero.
        private const int BcryptMaxPasswordBytes = 72;

        private readonly PasswordHasher _hasher = new();

        // --- HashPassword ---

        [Fact]
        public void HashPassword_ReturnsBcryptHashAndNotThePassword()
        {
            // Il risultato deve essere l'hash bcrypt, non la password: se il fattore di lavoro
            // scendesse, questo test segnalerebbe che la protezione si e' indebolita.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.NotEqual(TestPassword, hash);
            Assert.StartsWith($"{BcryptPrefix}{BcryptWorkFactor}$", hash);
            Assert.Equal(BcryptHashLength, hash.Length);
        }

        [Fact]
        public void HashPassword_TwiceOnTheSamePassword_ReturnsDifferentHashes()
        {
            // Il sale e' nuovo a ogni chiamata: due utenti con la stessa password non devono avere lo
            // stesso hash, altrimenti chi legge la tabella puo' capire chi condivide la password.
            // Entrambi gli hash restano verificabili, perche' il sale viaggia dentro l'hash.
            var first = _hasher.HashPassword(TestPassword);
            var second = _hasher.HashPassword(TestPassword);

            Assert.NotEqual(first, second);
            Assert.True(_hasher.VerifyPassword(first, TestPassword));
            Assert.True(_hasher.VerifyPassword(second, TestPassword));
        }

        [Fact]
        public void HashPassword_ThenVerify_RoundTripAsUserServiceDoes_ReturnsTrue()
        {
            // Il giro esatto di UserService: RegisterAsync hasha la password del model (riga 68) e
            // LoginAsync verifica l'hash salvato (riga 94). Se questo giro non torna, la registrazione
            // produce utenti che non riescono piu' ad autenticarsi.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.True(_hasher.VerifyPassword(hash, TestPassword));
        }

        [Fact]
        public void HashPassword_PasswordOf72Bytes_IsHashedAndVerifiable()
        {
            // Il limite di BCrypt e il limite del validator coincidono, quindi la password piu' lunga
            // ammessa arriva al hasher per intero. Valore ripetuto, cosi' non e' plausibile come
            // credenziale reale.
            var password = new string('x', BcryptMaxPasswordBytes);

            var hash = _hasher.HashPassword(password);

            Assert.True(_hasher.VerifyPassword(hash, password));
        }

        // --- VerifyPassword ---

        [Theory]
        [InlineData("password-di-test-1!")]   // differenza di maiuscole
        [InlineData("Password-di-test-1! ")]  // spazio finale
        [InlineData("Password-di-test-2!")]   // carattere diverso
        [InlineData("Password-di-test")]      // piu' corta
        [InlineData("Password-di-test-11!")]  // lunghezza diversa
        public void VerifyPassword_PasswordDifferentFromTheHashedOne_ReturnsFalse(string providedPassword)
        {
            // Nessun falso positivo: una password anche simile non deve mai passare. false e' l'unica
            // risposta che UserService sa tradurre in 401, quindi un true qui aprirebbe il sessione a
            // chi ha sbagliato la password.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.False(_hasher.VerifyPassword(hash, providedPassword));
        }

        [Fact]
        public void VerifyPassword_AcceptsHashCreatedWithADifferentWorkFactor()
        {
            // Il fattore di lavoro puo' cambiare nel tempo, per esempio perche' l'hardware e' piu'
            // veloce: gli hash gia' in tabella devono restare verificabili, altrimenti alzare il
            // fattore lascerebbe fuori tutti gli utenti registrati in precedenza. Qui il fattore
            // basso serve solo a mantenere il test veloce, non e' quello di produzione.
            var hashWithLowerWorkFactor = BCrypt.Net.BCrypt.HashPassword(TestPassword, 4);

            Assert.True(_hasher.VerifyPassword(hashWithLowerWorkFactor, TestPassword));
        }

        [Fact]
        public void VerifyPassword_ArgumentsSwapped_ThrowsSaltParseException()
        {
            // L'ordine del wrapper e' invertito rispetto a BCrypt.Verify(text, hash): passando i due
            // argomenti al contrario, la password in chiaro viene letta come hash salvato e BCrypt
            // solleva un'eccezione invece di restituire false. Il test rende esplicita la trappola:
            // un chiamante che sbaglia l'ordine fallisce subito invece di autenticare l'utente
            // sbagliato. In UserService la forma corretta e' VerifyPassword(user.PasswordHash, password).
            var hash = _hasher.HashPassword(TestPassword);

            Assert.Throws<SaltParseException>(() => _hasher.VerifyPassword(TestPassword, hash));
        }

        [Fact]
        public void VerifyPassword_StoredHashNotGeneratedByBcrypt_ThrowsSaltParseException()
        {
            // Un hash salvato che non e' un hash bcrypt (dato corrotto, colonna in chiaro per errore,
            // formato di una versione diversa) fa lanciare invece di rispondere false. In LoginAsync
            // questo diventa un errore 500 e non un 401: il comportamento e' documentato qui, ma va
            // ricordato quando si decide come trattare i dati corrotti.
            Assert.Throws<SaltParseException>(() => _hasher.VerifyPassword("non-e-un-hash-bcrypt", TestPassword));
        }
    }
}
