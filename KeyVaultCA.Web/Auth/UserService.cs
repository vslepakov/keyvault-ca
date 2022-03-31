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
        private readonly AuthConfiguration _configuration;
        private readonly List<User> _users;

        public UserService(AuthConfiguration configuration)
        {
            _configuration = configuration;
            _users = new()
            {
                new User { Id = 1, FirstName = "Max", LastName = "Mustermann", Username = _configuration.EstUsername, Password = _configuration.EstPassword }
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
