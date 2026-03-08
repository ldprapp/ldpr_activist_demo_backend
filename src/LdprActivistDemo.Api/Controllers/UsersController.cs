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
	private readonly IActorAccessService _actorAccess;

	public UsersController(IUserService users, IOtpService otp, IPasswordResetService passwordReset, IImageService images, IActorAccessService actorAccess)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_otp = otp ?? throw new ArgumentNullException(nameof(otp));
		_passwordReset = passwordReset ?? throw new ArgumentNullException(nameof(passwordReset));
		_images = images ?? throw new ArgumentNullException(nameof(images));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
	}

	[HttpPost("register")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(UserIdResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
	public async Task<ActionResult<UserIdResponse>> Register([FromForm] RegisterUserFormRequest request, CancellationToken cancellationToken)
	{
		if(request.AvatarImage is not null)
		{
			var avatarValidationError = UploadedImageReader.ValidateImage(request.AvatarImage);
			if(avatarValidationError is not null)
			{
				return this.ValidationProblemWithCode(
					ApiErrorCodes.ValidationFailed,
					new Dictionary<string, string[]>
					{
						["avatarImage"] = new[] { avatarValidationError },
					});
			}
		}

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
					RegionName: request.RegionName,
					CityName: request.CityName,
					AvatarImageId: null),
				cancellationToken);

			if(request.AvatarImage is not null)
			{
				var img = await UploadedImageReader.ReadAsync(request.AvatarImage, cancellationToken);
				var avatarImageId = await _images.CreateAsync(userId, img, cancellationToken);
				await _users.SetAvatarImageAsync(userId, avatarImageId, cancellationToken);
			}

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
	public async Task<IActionResult> ChangePassword(
		Guid id,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		[FromBody] ChangePasswordRequest request,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var invalidActorTarget = this.TryBuildActorUserMatchValidationProblem(actorUserId, id, nameof(id));
		if(invalidActorTarget is not null)
		{
			return invalidActorTarget;
		}

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

		var invalidPasswordMatch = this.TryBuildActorPasswordMatchValidationProblem(
			actorUserPassword,
			request.OldPassword,
			nameof(request.OldPassword),
			ActorPasswordHeader);
		if(invalidPasswordMatch is not null)
		{
			return invalidPasswordMatch;
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверные учётные данные.", $"Проверьте actorUserId и заголовок {ActorPasswordHeader}.");
		}

		var ok = await _users.ChangePasswordAsync(id, request.NewPassword, cancellationToken);
		if(!ok)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
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
		[FromQuery] Guid actorUserId,
		[FromForm] UpdateUserFormRequest request,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var invalidActorTarget = this.TryBuildActorUserMatchValidationProblem(actorUserId, id, nameof(id));
		if(invalidActorTarget is not null)
		{
			return invalidActorTarget;
		}

		var errors = ValidateUpdate(request);
		if(errors.Count > 0)
		{
			return this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверные учётные данные.", $"Проверьте actorUserId и заголовок {ActorPasswordHeader}.");
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
				avatarImageId = await _images.CreateAsync(actorUserId, img, cancellationToken);
			}

			var ok = await _users.UpdateAsync(
				new UserUpdateModel(
					UserId: id,
					LastName: request.LastName,
					FirstName: request.FirstName,
					MiddleName: request.MiddleName,
					Gender: request.Gender,
					BirthDate: request.BirthDate,
					RegionName: request.RegionName,
					CityName: request.CityName,
					AvatarImageId: avatarImageId),
				cancellationToken);

			if(!ok)
			{
				return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
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
		[FromQuery] Guid actorUserId,
		[FromBody] ChangePhoneRequest request,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var invalidActorTarget = this.TryBuildActorUserMatchValidationProblem(actorUserId, id, nameof(id));
		if(invalidActorTarget is not null)
		{
			return invalidActorTarget;
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

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

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword!, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return this.ProblemWithCode(StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials, "Неверные учётные данные.", $"Проверьте actorUserId и заголовок {ActorPasswordHeader}.");
		}

		try
		{
			var ok = await _users.ChangePhoneAsync(
				id,
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

	[HttpGet("feed")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<ActionResult<IReadOnlyList<UserDto>>> GetFeed(
		[FromQuery] string? role,
		[FromQuery] string? regionName,
		[FromQuery] string? cityName,
		[FromQuery] int? start,
		[FromQuery] int? end,
		CancellationToken cancellationToken)
	{
		var invalidFilters = TryBuildUsersFeedFilterValidationProblem(role, regionName, cityName);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		var invalidPagination = TryBuildUsersPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		UserRoleRules.TryNormalizeOptionalRole(role, out var normalizedRole, out _);

		var users = await _users.GetUsersAsync(normalizedRole, regionName, cityName, cancellationToken);
		var dtos = users.Select(ToDto).ToList();
		return Ok(ApplyUsersPagination(dtos, start, end));
	}

	[HttpGet("{id:guid}/role")]
	[ProducesResponseType(typeof(UserRoleResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<UserRoleResponse>> GetRole(Guid id, CancellationToken cancellationToken)
	{
		var role = await _users.GetRoleAsync(id, cancellationToken);
		if(role is null)
		{
			return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
		}

		return Ok(new UserRoleResponse(role));
	}

	[HttpPut("{id:guid}/role/coordinator")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> GrantCoordinatorRole(
		Guid id,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _users.SetCoordinatorRoleAsync(actorUserId, actorUserPassword!, id, isCoordinator: true, cancellationToken);
		return result.IsSuccess ? NoContent() : MapUserRoleChangeError(result.Error);
	}

	[HttpDelete("{id:guid}/role/coordinator")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RevokeCoordinatorRole(
		Guid id,
		[FromQuery] Guid actorUserId,
		[FromHeader(Name = ActorPasswordHeader)] string? actorUserPassword,
		CancellationToken cancellationToken)
	{
		var invalidActor = this.TryBuildActorRequestValidationProblem(actorUserId, actorUserPassword, ActorPasswordHeader);
		if(invalidActor is not null)
		{
			return invalidActor;
		}

		var result = await _users.SetCoordinatorRoleAsync(actorUserId, actorUserPassword!, id, isCoordinator: false, cancellationToken);
		return result.IsSuccess ? NoContent() : MapUserRoleChangeError(result.Error);
	}

	private static UserDto ToDto(UserPublicModel u) => new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.BirthDate,
			u.RegionName,
			u.CityName,
			u.Role,
			u.IsPhoneConfirmed)
	{
		AvatarImageUrl = u.AvatarImageUrl,
	};

	private ActionResult? TryBuildUsersFeedFilterValidationProblem(string? role, string? regionName, string? cityName)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(!UserRoleRules.TryNormalizeOptionalRole(role, out _, out var roleError))
		{
			errors["role"] = new[] { roleError! };
		}

		if(regionName is not null && string.IsNullOrWhiteSpace(regionName))
		{
			errors["regionName"] = new[] { "RegionName must not be empty." };
		}

		if(cityName is not null)
		{
			var list = new List<string>();

			if(string.IsNullOrWhiteSpace(cityName))
			{
				list.Add("CityName must not be empty.");
			}

			if(string.IsNullOrWhiteSpace(regionName))
			{
				list.Add("cityName can be used only together with regionName.");
			}

			if(list.Count > 0)
			{
				errors["cityName"] = list.ToArray();
			}
		}

		return errors.Count == 0 ? null : this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors, title: "Некорректный запрос.", detail: $"Проверьте параметры role/regionName/cityName. role допускает только '{UserRoles.Activist}', '{UserRoles.Coordinator}' или '{UserRoles.Admin}', cityName допускается только вместе с regionName.");
	}



	private ActionResult? TryBuildUsersPaginationValidationProblem(int? start, int? end)
	{
		if(start is null && end is null)
		{
			return null;
		}

		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

		if(start is null || end is null)
		{
			if(start is null)
			{
				errors["start"] = new[] { "Start is required when end is specified." };
			}

			if(end is null)
			{
				errors["end"] = new[] { "End is required when start is specified." };
			}

			return (ActionResult)this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Параметры start и end должны быть указаны вместе.");
		}

		if(start.Value <= 0)
		{
			errors["start"] = new[] { "Start must be positive." };
		}

		if(end.Value <= 0)
		{
			errors["end"] = new[] { "End must be positive." };
		}

		if(errors.Count == 0 && end.Value < start.Value)
		{
			errors["end"] = new[] { "End must be greater or equal to start." };
		}

		return errors.Count == 0
			? null
			: (ActionResult)this.ValidationProblemWithCode(
				ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Проверьте параметры start и end (нумерация с 1, end должен быть >= start).");
	}

	private static IReadOnlyList<T> ApplyUsersPagination<T>(IReadOnlyList<T> items, int? start, int? end)
	{
		if(start is null || end is null)
		{
			return items;
		}

		var skip = start.Value - 1;
		var take = end.Value - start.Value + 1;

		if(skip < 0 || take <= 0)
		{
			return Array.Empty<T>();
		}

		return items
			.Skip(skip)
			.Take(take)
			.ToList();
	}

	private IActionResult MapUserRoleChangeError(UserRoleChangeError error)
		=> error switch
		{
			UserRoleChangeError.ValidationFailed => this.ProblemWithCode(
				StatusCodes.Status400BadRequest,
				ApiErrorCodes.ValidationFailed,
				"Некорректный запрос.",
				"Проверьте actorUserId, заголовок пароля и id пользователя."),

			UserRoleChangeError.InvalidCredentials => this.ProblemWithCode(
				StatusCodes.Status401Unauthorized,
				ApiErrorCodes.InvalidCredentials,
				"Неверные учётные данные.",
				$"Проверьте actorUserId и заголовок {ActorPasswordHeader}."),

			UserRoleChangeError.Forbidden => this.ProblemWithCode(
				StatusCodes.Status403Forbidden,
				ApiErrorCodes.Forbidden,
				"Нет доступа.",
				"Операция доступна только пользователю с ролью admin."),

			UserRoleChangeError.UserNotFound => this.ProblemWithCode(
				StatusCodes.Status404NotFound,
				ApiErrorCodes.UserNotFound,
				"Пользователь не найден."),

			UserRoleChangeError.RoleChangeNotAllowed => this.ProblemWithCode(
				StatusCodes.Status409Conflict,
				ApiErrorCodes.UserRoleChangeNotAllowed,
				"Изменение роли запрещено.",
				"Нельзя выдавать или забирать роль coordinator у пользователя с ролью admin."),

			_ => this.ProblemWithCode(
				StatusCodes.Status500InternalServerError,
				ApiErrorCodes.InternalError,
				"Внутренняя ошибка."),
		};

	public sealed class RegisterUserFormRequest
	{
		public string LastName { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string? MiddleName { get; set; }
		public string? Gender { get; set; }
		public string PhoneNumber { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public DateOnly BirthDate { get; set; }
		public string RegionName { get; set; } = string.Empty;
		public string CityName { get; set; } = string.Empty;

		public IFormFile? AvatarImage { get; set; }
	}

	public sealed class UpdateUserFormRequest
	{
		public string LastName { get; set; } = string.Empty;
		public string FirstName { get; set; } = string.Empty;
		public string? MiddleName { get; set; }
		public string? Gender { get; set; }
		public DateOnly BirthDate { get; set; }
		public string RegionName { get; set; } = string.Empty;
		public string CityName { get; set; } = string.Empty;

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
		if(string.IsNullOrWhiteSpace(r.RegionName)) errors[nameof(r.RegionName)] = new[] { "RegionName is required." };
		if(string.IsNullOrWhiteSpace(r.CityName)) errors[nameof(r.CityName)] = new[] { "CityName is required." };

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
		if(string.IsNullOrWhiteSpace(r.RegionName)) errors[nameof(r.RegionName)] = new[] { "RegionName is required." };
		if(string.IsNullOrWhiteSpace(r.CityName)) errors[nameof(r.CityName)] = new[] { "CityName is required." };

		return errors;
	}

	private static bool IsPhoneDuplicate(InvalidOperationException ex) =>
		ex.Message.Contains("PhoneNumber already exists", StringComparison.OrdinalIgnoreCase);

	private static bool IsCityMismatch(InvalidOperationException ex) =>
		ex.Message.Contains("City does not belong", StringComparison.OrdinalIgnoreCase);

	private static bool IsGenderInvalid(InvalidOperationException ex) =>
		ex.Message.Contains("Gender is invalid", StringComparison.OrdinalIgnoreCase);
}