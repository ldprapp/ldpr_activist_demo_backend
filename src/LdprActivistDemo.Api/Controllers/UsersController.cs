using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Errors;
using LdprActivistDemo.Contracts.Users;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
	private const string ActorPasswordHeader = "X-Actor-Password";

	private readonly IUserService _users;

	public UsersController(IUserService users)
	{
		_users = users;
	}

	[HttpPost("register")]
	[ProducesResponseType(typeof(UserIdResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<UserIdResponse>> Register([FromBody] UserRegisterRequest request, CancellationToken cancellationToken)
	{
		var errors = ValidateRegister(request);
		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		try
		{
			var userId = await _users.RegisterAsync(
				new UserCreateModel(
					LastName: request.LastName,
					FirstName: request.FirstName,
					MiddleName: request.MiddleName,
					Gender: request.Gender,
					PhoneNumber: request.PhoneNumber,
					Password: request.Password,
					BirthDate: request.BirthDate,
					RegionId: request.RegionId,
					CityId: request.CityId),
				cancellationToken);

			return Created($"/api/v1/users/{userId}", new UserIdResponse(userId));
		}
		catch(InvalidOperationException ex) when(IsPhoneDuplicate(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.PhoneAlreadyExists, "Телефон уже занят.", "Пользователь с таким номером телефона уже существует.");
		}
		catch(InvalidOperationException ex) when(IsCityMismatch(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.CityRegionMismatch, "Некорректный город/регион.", "Указанный город не принадлежит региону или не существует.");
		}
		catch(InvalidOperationException ex) when(IsGenderInvalid(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.GenderInvalid, "Некорректный пол.", "Допустимые значения: 'male', 'female' или null.");
		}
		catch(Exception)
		{
			return this.ProblemWithCode(StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError, "Внутренняя ошибка.");
		}
	}

	[HttpPost("confirm-phone")]
	[ProducesResponseType(typeof(ConfirmPhoneResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<ActionResult<ConfirmPhoneResponse>> ConfirmPhone([FromBody] ConfirmPhoneRequest request, CancellationToken cancellationToken)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber is required." };
		}
		else if(!IsValidPhoneNumber(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber has invalid format." };
		}

		if(string.IsNullOrWhiteSpace(request.OtpCode))
		{
			errors[nameof(request.OtpCode)] = new[] { "OtpCode is required." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors);
		}

		var ok = await _users.ConfirmPhoneAsync(request.PhoneNumber, request.OtpCode, cancellationToken);
		if(!ok)
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.OtpInvalid, "Неверный код.", "OTP не найден/истёк или не совпадает.");
		}

		return Ok(new ConfirmPhoneResponse(true));
	}

	[HttpPost("login")]
	[ProducesResponseType(typeof(LoginOkResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<LoginOkResponse>> Login([FromBody] UserLoginRequest request, CancellationToken cancellationToken)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber is required." };
		}
		else if(!IsValidPhoneNumber(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber has invalid format." };
		}

		if(string.IsNullOrWhiteSpace(request.Password))
		{
			errors[nameof(request.Password)] = new[] { "Password is required." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors);
		}

		var result = await _users.LoginAsync(request.PhoneNumber, request.Password, cancellationToken);
		if(result.IsSuccess)
		{
			return Ok(new LoginOkResponse(true));
		}

		return result.Error switch
		{
			LoginError.PhoneNotConfirmed => this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.PhoneNotConfirmed, "Телефон не подтверждён.", "Сначала подтвердите телефон через OTP."),
			_ => this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверные учётные данные.", "Телефон не найден или пароль неверный."),
		};
	}

	[HttpGet("by-phone/{phoneNumber}")]
	[ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UserDto>> GetByPhone(string phoneNumber, CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(phoneNumber) || !IsValidPhoneNumber(phoneNumber))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					[nameof(phoneNumber)] = new[] { "PhoneNumber has invalid format." },
				});
		}

		var u = await _users.GetByPhoneAsync(phoneNumber, cancellationToken);
		if(u is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		return Ok(ToDto(u));
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
	{
		var u = await _users.GetByIdAsync(id, cancellationToken);
		if(u is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		return Ok(ToDto(u));
	}

	[HttpPut("{id:guid}/password")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					[nameof(request.OldPassword)] = new[] { "OldPassword is required." },
					[nameof(request.NewPassword)] = new[] { "NewPassword is required." },
				});
		}

		var existing = await _users.GetByIdAsync(id, cancellationToken);
		if(existing is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		var ok = await _users.ChangePasswordAsync(id, request.OldPassword, request.NewPassword, cancellationToken);
		if(!ok)
		{
			return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверный пароль.", "OldPassword не совпадает.");
		}

		return NoContent();
	}

	[HttpPut("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Update(
		Guid id,
		[FromBody] UpdateUserRequest request,
		[FromHeader(Name = ActorPasswordHeader)] string? actorPassword,
		CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(actorPassword))
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["ActorPassword"] = new[] { "ActorPassword is required (use X-Actor-Password header)." },
				});
		}

		var errors = ValidateUpdate(request);
		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		var existing = await _users.GetByIdAsync(id, cancellationToken);
		if(existing is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		try
		{
			var ok = await _users.UpdateAsync(
				new UserUpdateModel(
					UserId: id,
					LastName: request.LastName,
					FirstName: request.FirstName,
					MiddleName: request.MiddleName,
					Gender: request.Gender,
					BirthDate: request.BirthDate,
					RegionId: request.RegionId,
					CityId: request.CityId),
				actorPassword,
				cancellationToken);

			if(!ok)
			{
				return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверный пароль.", "ActorPassword не совпадает.");
			}

			return NoContent();
		}
		catch(InvalidOperationException ex) when(IsCityMismatch(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.CityRegionMismatch, "Некорректный город/регион.", "Указанный город не принадлежит региону или не существует.");
		}
		catch(InvalidOperationException ex) when(IsGenderInvalid(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.GenderInvalid, "Некорректный пол.", "Допустимые значения: 'male', 'female' или null.");
		}
	}

	[HttpPut("{id:guid}/phone")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ChangePhone(
		Guid id,
		[FromBody] ChangePhoneRequest request,
		[FromHeader(Name = ActorPasswordHeader)] string? actorPassword,
		CancellationToken cancellationToken)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(actorPassword))
		{
			errors["ActorPassword"] = new[] { "ActorPassword is required (use X-Actor-Password header)." };
		}

		if(string.IsNullOrWhiteSpace(request.NewPhoneNumber))
		{
			errors[nameof(request.NewPhoneNumber)] = new[] { "NewPhoneNumber is required." };
		}
		else if(!IsValidPhoneNumber(request.NewPhoneNumber))
		{
			errors[nameof(request.NewPhoneNumber)] = new[] { "NewPhoneNumber has invalid format." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors);
		}

		var existing = await _users.GetByIdAsync(id, cancellationToken);
		if(existing is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		try
		{
			var ok = await _users.ChangePhoneAsync(id, actorPassword!, request.NewPhoneNumber, cancellationToken);

			if(!ok)
			{
				return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверный пароль.", "ActorPassword не совпадает.");
			}

			return NoContent();
		}
		catch(InvalidOperationException ex) when(IsPhoneDuplicate(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.PhoneAlreadyExists, "Телефон уже занят.", "Пользователь с таким номером телефона уже существует.");
		}
	}

	[HttpGet("by-region/{regionId:int}")]
	[ProducesResponseType(typeof(IReadOnlyList<UserFullNameDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<UserFullNameDto>>> GetByRegion(int regionId, CancellationToken cancellationToken)
	{
		var users = await _users.GetUsersByRegionAsync(regionId, cancellationToken);
		var dto = users.Select(x => new UserFullNameDto(x.LastName, x.FirstName, x.MiddleName)).ToList();
		return Ok(dto);
	}

	[HttpGet("by-city/{regionId:int}/{cityId:int}")]
	[ProducesResponseType(typeof(IReadOnlyList<UserFullNameDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<UserFullNameDto>>> GetByCity(int regionId, int cityId, CancellationToken cancellationToken)
	{
		var users = await _users.GetUsersByRegionAndCityAsync(regionId, cityId, cancellationToken);
		var dto = users.Select(x => new UserFullNameDto(x.LastName, x.FirstName, x.MiddleName)).ToList();
		return Ok(dto);
	}

	[HttpGet("{id:guid}/is-admin")]
	[ProducesResponseType(typeof(IsAdminResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<IsAdminResponse>> IsAdmin(Guid id, CancellationToken cancellationToken)
	{
		var existing = await _users.GetByIdAsync(id, cancellationToken);
		if(existing is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		var isAdmin = await _users.IsAdminAsync(id, cancellationToken);
		return Ok(new IsAdminResponse(isAdmin));
	}

	[HttpGet("admin-ids")]
	[ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<Guid>>> GetAdminIds(CancellationToken cancellationToken)
	{
		var ids = await _users.GetAdminIdsAsync(cancellationToken);
		return Ok(ids);
	}

	private static UserDto ToDto(UserPublicModel u) => new(
		u.Id,
		u.LastName,
		u.FirstName,
		u.MiddleName,
		u.Gender,
		u.PhoneNumber,
		u.BirthDate,
		u.RegionId,
		u.CityId,
		u.IsPhoneConfirmed,
		u.Points);

	private static Dictionary<string, string[]> ValidateRegister(UserRegisterRequest r)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(r.LastName)) errors[nameof(r.LastName)] = new[] { "LastName is required." };
		if(string.IsNullOrWhiteSpace(r.FirstName)) errors[nameof(r.FirstName)] = new[] { "FirstName is required." };
		if(string.IsNullOrWhiteSpace(r.PhoneNumber)) errors[nameof(r.PhoneNumber)] = new[] { "PhoneNumber is required." };
		else if(!IsValidPhoneNumber(r.PhoneNumber)) errors[nameof(r.PhoneNumber)] = new[] { "PhoneNumber has invalid format." };
		if(string.IsNullOrWhiteSpace(r.Password)) errors[nameof(r.Password)] = new[] { "Password is required." };
		if(r.RegionId <= 0) errors[nameof(r.RegionId)] = new[] { "RegionId must be positive." };
		if(r.CityId <= 0) errors[nameof(r.CityId)] = new[] { "CityId must be positive." };

		return errors;
	}

	private static bool IsValidPhoneNumber(string? value)
	{
		value = (value ?? string.Empty).Trim();
		if(value.Length == 0)
		{
			return false;
		}

		var digits = 0;
		var plusSeen = false;

		for(var i = 0; i < value.Length; i++)
		{
			var ch = value[i];

			if(ch >= '0' && ch <= '9')
			{
				digits++;
				continue;
			}

			if(ch == '+')
			{
				if(i != 0 || plusSeen)
				{
					return false;
				}

				plusSeen = true;
				continue;
			}

			if(ch == ' ' || ch == '-' || ch == '(' || ch == ')')
			{
				continue;
			}

			return false;
		}

		return digits >= 10 && digits <= 15;
	}

	private static Dictionary<string, string[]> ValidateUpdate(UpdateUserRequest r)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(r.LastName)) errors[nameof(r.LastName)] = new[] { "LastName is required." };
		if(string.IsNullOrWhiteSpace(r.FirstName)) errors[nameof(r.FirstName)] = new[] { "FirstName is required." };
		if(r.RegionId <= 0) errors[nameof(r.RegionId)] = new[] { "RegionId must be positive." };
		if(r.CityId <= 0) errors[nameof(r.CityId)] = new[] { "CityId must be positive." };

		return errors;
	}

	private static bool IsPhoneDuplicate(InvalidOperationException ex) =>
		ex.Message.Contains("PhoneNumber already exists", StringComparison.OrdinalIgnoreCase);

	private static bool IsCityMismatch(InvalidOperationException ex) =>
		ex.Message.Contains("City does not belong", StringComparison.OrdinalIgnoreCase);

	private static bool IsGenderInvalid(InvalidOperationException ex) =>
		ex.Message.Contains("Gender is invalid", StringComparison.OrdinalIgnoreCase);
}