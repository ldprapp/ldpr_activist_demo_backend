using LdprActivistDemo.Api.Errors;
using LdprActivistDemo.Api.Helpers;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.PasswordReset;
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
	private readonly IOtpService _otp;
	private readonly IPasswordResetService _passwordReset;
	private readonly IImageService _images;

	public UsersController(IUserService users, IOtpService otp, IPasswordResetService passwordReset, IImageService images)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_otp = otp ?? throw new ArgumentNullException(nameof(otp));
		_passwordReset = passwordReset ?? throw new ArgumentNullException(nameof(passwordReset));
		_images = images ?? throw new ArgumentNullException(nameof(images));
	}

	[HttpPost("register")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(UserIdResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<UserIdResponse>> Register([FromForm] RegisterUserFormRequest request, CancellationToken cancellationToken)
	{
		var errors = ValidateRegister(request);
		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		try
		{
			Guid? avatarImageId = null;
			if(request.AvatarImage is not null)
			{
				var err = UploadedImageReader.ValidateImage(request.AvatarImage);
				if(err is not null)
				{
					return this.ValidationProblemWithCode(
						ApiErrorCodes.ValidationFailed,
						new Dictionary<string, string[]>
						{
							["avatarImage"] = new[] { err },
						});
				}

				var img = await UploadedImageReader.ReadAsync(request.AvatarImage, cancellationToken);
				avatarImageId = await _images.CreateAsync(img, cancellationToken);
			}

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
					CityId: request.CityId,
					AvatarImageId: avatarImageId),
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

	[HttpPost("send-otp")]
	[ProducesResponseType(typeof(SendOtpResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<SendOtpResponse>> SendOtp([FromBody] SendOtpRequest request, CancellationToken cancellationToken)
	{
		if(request is null)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request body is required." },
				});
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber is required." };
		}
		else if(!IsValidPhoneNumber(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber has invalid format." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		var u = await _users.GetByPhoneAsync(request.PhoneNumber, cancellationToken);
		if(u is not null && u.IsPhoneConfirmed)
		{
			return this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.PhoneAlreadyConfirmed,
				"Телефон уже подтверждён.",
				"Повторная отправка OTP для уже подтверждённого номера запрещена.");
		}

		try
		{
			await _otp.IssueAsync(request.PhoneNumber, cancellationToken);
			return Ok(new SendOtpResponse(true));
		}
		catch(Exception)
		{
			return this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.OtpSendFailed,
				"Не удалось отправить OTP.",
				"Ошибка отправки OTP (проверьте провайдера доставки/SMS).");
		}
	}

	[HttpPost("password-reset/request")]
	[ProducesResponseType(typeof(RequestPasswordResetResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<RequestPasswordResetResponse>> RequestPasswordReset(
		[FromBody] RequestPasswordResetRequest request,
		CancellationToken cancellationToken)
	{
		if(request is null)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request body is required." },
				});
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(string.IsNullOrWhiteSpace(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber is required." };
		}
		else if(!IsValidPhoneNumber(request.PhoneNumber))
		{
			errors[nameof(request.PhoneNumber)] = new[] { "PhoneNumber has invalid format." };
		}

		if(string.IsNullOrWhiteSpace(request.NewPassword))
		{
			errors[nameof(request.NewPassword)] = new[] { "NewPassword is required." };
		}

		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		var result = await _passwordReset.IssueAsync(request.PhoneNumber, request.NewPassword, cancellationToken);

		if(result.IsSuccess)
		{
			return Ok(new RequestPasswordResetResponse(true));
		}

		return result.Error switch
		{
			PasswordResetIssueError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден.",
				"Аккаунт с таким номером телефона не существует."),

			PasswordResetIssueError.PhoneNotConfirmed => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.PhoneNotConfirmed,
				"Телефон не подтверждён.",
				"Смена пароля доступна только для подтверждённого номера."),

			PasswordResetIssueError.OtpSendFailed => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.OtpSendFailed,
				"Не удалось отправить OTP.",
				"Ошибка отправки OTP (проверьте провайдера доставки/SMS)."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}

	[HttpPost("password-reset/confirm")]
	[ProducesResponseType(typeof(ConfirmPasswordResetResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<ConfirmPasswordResetResponse>> ConfirmPasswordReset(
		[FromBody] ConfirmPasswordResetRequest request,
		CancellationToken cancellationToken)
	{
		if(request is null)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request body is required." },
				});
		}

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
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		var result = await _passwordReset.ConfirmAsync(request.PhoneNumber, request.OtpCode, cancellationToken);

		if(result.IsSuccess)
		{
			return Ok(new ConfirmPasswordResetResponse(true));
		}

		return result.Error switch
		{
			PasswordResetConfirmError.OtpInvalid => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.OtpInvalid,
				"Неверный код.",
				"OTP не найден/истёк или не совпадает."),

			PasswordResetConfirmError.PasswordResetExpired => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.PasswordResetExpired,
				"Запрос на смену пароля истёк.",
				"Повторите запрос на смену пароля."),

			PasswordResetConfirmError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			PasswordResetConfirmError.PhoneNotConfirmed => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.PhoneNotConfirmed,
				"Телефон не подтверждён.",
				"Смена пароля доступна только для подтверждённого номера."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};
	}

	[HttpPost("confirm-phone")]
	[ProducesResponseType(typeof(ConfirmPhoneResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<ActionResult<ConfirmPhoneResponse>> ConfirmPhone([FromBody] ConfirmPhoneRequest request, CancellationToken cancellationToken)
	{
		if(request is null)
		{
			return this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				new Dictionary<string, string[]>
				{
					["body"] = new[] { "Request body is required." },
				});
		}

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

		var existingUser = await _users.GetByPhoneAsync(request.PhoneNumber, cancellationToken);
		if(existingUser is not null && existingUser.IsPhoneConfirmed)
		{
			return this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.PhoneAlreadyConfirmed,
				"Телефон уже подтверждён.",
				"Повторное подтверждение телефона не требуется.");
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
	[Consumes("multipart/form-data")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Update(
		Guid id,
		[FromForm] UpdateUserFormRequest request,
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
			Guid? avatarImageId = null;
			if(request.AvatarImage is not null)
			{
				var err = UploadedImageReader.ValidateImage(request.AvatarImage);
				if(err is not null)
				{
					return this.ValidationProblemWithCode(
						ApiErrorCodes.ValidationFailed,
						new Dictionary<string, string[]>
						{
							["avatarImage"] = new[] { err },
						});
				}

				var img = await UploadedImageReader.ReadAsync(request.AvatarImage, cancellationToken);
				avatarImageId = await _images.CreateAsync(img, cancellationToken);
			}

			var ok = await _users.UpdateAsync(
				new UserUpdateModel(
					UserId: id,
					LastName: request.LastName,
					FirstName: request.FirstName,
					MiddleName: request.MiddleName,
					Gender: request.Gender,
					BirthDate: request.BirthDate,
					RegionId: request.RegionId,
					CityId: request.CityId,
					AvatarImageId: avatarImageId),
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

		var existing = await _users.GetByIdAsync(id, cancellationToken);
		if(existing is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		try
		{
			var ok = await _users.ChangePhoneAsync(
				id,
				actorPassword!,
				request.NewPhoneNumber,
				request.OtpCode,
				cancellationToken);

			if(!ok)
			{
				return this.ProblemWithCode(
					StatusCodes.Status400BadRequest,
					ApiErrorCodes.OtpInvalid,
					"Неверный код.",
					"OTP не найден, истёк или не совпадает.");
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

	[HttpGet("admins")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAdmins(CancellationToken cancellationToken)
	{
		var admins = await _users.GetAdminsAsync(cancellationToken);
		return Ok(admins.Select(ToDto).ToList());
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

	public sealed class RegisterUserFormRequest
	{
		public string LastName { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string? MiddleName { get; set; }
		public string? Gender { get; set; }
		public string PhoneNumber { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public DateOnly BirthDate { get; set; }
		public int RegionId { get; set; }
		public int CityId { get; set; }

		public IFormFile? AvatarImage { get; set; }
	}

	public sealed class UpdateUserFormRequest
	{
		public string LastName { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string? MiddleName { get; set; }
		public string? Gender { get; set; }
		public DateOnly BirthDate { get; set; }
		public int RegionId { get; set; }
		public int CityId { get; set; }

		public IFormFile? AvatarImage { get; set; }
	}

	private static Dictionary<string, string[]> ValidateRegister(RegisterUserFormRequest r)
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

	private static Dictionary<string, string[]> ValidateUpdate(UpdateUserFormRequest r)
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