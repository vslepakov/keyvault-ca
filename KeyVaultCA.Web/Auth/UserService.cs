using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KeyVaultCA.Web.Auth
{
    public interface IUserService
    {
        Task<User> Authenticate(string username, string password);
        Task<IEnumerable<User>> GetAll();
    }

    public class UserService : IUserService
    {
        private readonly CAConfiguration _confuguration;
        private readonly List<User> _users;

        public UserService(CAConfiguration confuguration)
        {
            _confuguration = confuguration;
            _users = new()
            {
                new User { Id = 1, FirstName = "Max", LastName = "Mustermann", Username = _confuguration.EstUsername, Password = _confuguration.EstPassword }
            };
        }

        public async Task<User> Authenticate(string username, string password)
        {
            var user = await Task.Run(() => _users.SingleOrDefault(x => x.Username == username && x.Password == password));

            // return null if user not found
            if (user == null)
                return null;

            // authentication successful so return user details without password
            return user.WithoutPassword();
        }

        public async Task<IEnumerable<User>> GetAll()
        {
            return await Task.Run(() => _users.WithoutPasswords());
        }
    }
}
