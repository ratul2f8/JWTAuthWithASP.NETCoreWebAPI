using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dotnetcoreJWT.Models;
using dotnetcoreJWT.Utils;
using dotnetcoreJWT.Dto;
using dotnetcoreJWT.Security;
using Microsoft.AspNetCore.Authorization;

namespace dotnetcoreJWT.Controllers
{
    public class GetTokenRequest
    {
        public string Email { get; set; }
    }
    public class ChangePasswordRequest
    {
        public string Token { get; set; }
        public string Password { get; set; }
    }
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJWTAuthenticationManager _jWTAuthenticationManager;

        public UsersController(ApplicationDbContext context,
            IJWTAuthenticationManager jWTAuthenticationManager)
        {
            _context = context;
            _jWTAuthenticationManager = jWTAuthenticationManager;
        }

        // GET: api/Users
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        //{
        //    return await _context.Users.ToListAsync();
        //}

        // GET: api/Users/5
        //[HttpGet("{id}")]
        //public async Task<ActionResult<User>> GetUser(Guid id)
        //{
        //    var user = await _context.Users.FindAsync(id);

        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    return user;
        //}

        // PUT: api/Users/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(Guid id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }
            user.UpdatedAt = DateTime.Now;
            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<UserCreatedResponseModel>> PostUser(User user)
        {
            if (!Validators.IsEmailValid(user.Email))
            {
                return BadRequest("Email is not valid!");
            }
            if (IsEmailTaken(user.Email))
            {
                return BadRequest("Email already taken!");
            }
            if (user.Password.Length <= 4)
            {
                return BadRequest("Length of the password must be greater than 4");
            }
            user.CreatedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserCreatedResponseModel()
            {
                Email = user.Email,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Id = user.Id
            };
        }
        [AllowAnonymous]
        [HttpPut("login")]
        public async Task<ActionResult<LoginResponseModel>> Login(LoginRequestModel model)
        {
            var user = _context.Users.Where(user => user.Email == model.Email).FirstOrDefault();
            if (user == null)
            {
                return NotFound("Email doesn't exist!");
            }
            var validPassword = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);
            if (!validPassword)
            {
                return BadRequest("Password don't match");
            }
            string token = _jWTAuthenticationManager.Authenticate(user);
            if (token == null)
            {
                return Unauthorized();
            }
            return new LoginResponseModel()
            {
                JWT = token,
                FullName = user.FullName,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Id = user.Id
            };
        }
        [AllowAnonymous]
        [HttpPut("gettoken")]
        public async Task<ActionResult<string>> GetTokenToSendEmail(GetTokenRequest req)
        {
            var user = _context.Users.Where(user => user.Email.Equals(req.Email)).FirstOrDefault();
            if (user == null)
            {
                return BadRequest();
            }
            else
            {
                Token newToken = new Token()
                {
                    UserId = user.Id,
                    ExpireAt = DateTime.UtcNow.AddMinutes(30)
                };
                _context.Tokens.Add(newToken);
                await _context.SaveChangesAsync();

                return newToken.Id.ToString();
            }
        }
        [AllowAnonymous]
        [HttpPut("changepassword")]
        public async Task<ActionResult> ChangePassword(ChangePasswordRequest req)
        {
            var parsedToken = Guid.Parse(req.Token);
            var foundToken = await _context.Tokens.FindAsync(parsedToken);
            if (foundToken == null)
            {
                return BadRequest("Token doesn't exist");
            }
            var expired = DateTime.Compare(DateTime.UtcNow, foundToken.ExpireAt);
            if (expired > 0)
            {
                return BadRequest("Token Expired");
            }
            var foundUser = await _context.Users.FindAsync(foundToken.UserId);
            if (foundUser == null)
            {
                return BadRequest("User doesn't exist");
            }
            if (req.Password.Length <= 4)
            {
                return BadRequest("Length of the password must be greater than 4");
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                DateTime timeNow = DateTime.Now;
                foundUser.UpdatedAt = timeNow;
                foundUser.Password = BCrypt.Net.BCrypt.HashPassword(req.Password.Trim());
                _context.Entry(foundUser).State = EntityState.Modified;
                _context.Tokens.Remove(foundToken);
                await _context.SaveChangesAsync();
                transaction.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return StatusCode(500);
            }
            return Ok();
        }
        // DELETE: api/Users/5
        //[HttpDelete("{id}")]
        //public async Task<IActionResult> DeleteUser(Guid id)
        //{
        //    var user = await _context.Users.FindAsync(id);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    _context.Users.Remove(user);
        //    await _context.SaveChangesAsync();

        //    return NoContent();
        //}

        private bool UserExists(Guid Id)
        {
            return _context.Users.Any(e => e.Id == Id);
        }
        private bool IsEmailTaken(string Email)
        {
            return _context.Users.Any(e => e.Email == Email);
        }

    }
}
