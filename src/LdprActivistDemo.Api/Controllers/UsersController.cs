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

/// <summary>
/// Эндпоинты регистрации, аутентификации, профиля пользователя и ролей.
/// </summary>
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

	public enum UserFeedResponseFormat
	{
		Users = 0,
		Count = 1,
	}

	/// <summary>
	/// Регистрирует нового пользователя.
	/// </summary>
	/// <remarks>
	/// Тело запроса передаётся как <c>multipart/form-data</c>, чтобы при необходимости
	/// можно было сразу загрузить аватар пользователя.
	/// </remarks>
	/// <param name="request">Данные регистрируемого пользователя.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="201">Пользователь успешно зарегистрирован.</response>
	/// <response code="400">Переданы некорректные данные регистрации или файла аватара.</response>
	/// <response code="409">Пользователь с таким номером телефона уже существует.</response>
	/// <response code="500">Произошла внутренняя ошибка сервера.</response>
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
					SettlementName: request.SettlementName,
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
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch(InvalidOperationException ex) when(IsPhoneDuplicate(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status409Conflict, ApiErrorCodes.PhoneAlreadyExists, "Телефон уже занят.", "Пользователь с таким номером телефона уже существует.");
		}
		catch(InvalidOperationException ex) when(IsSettlementMismatch(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.SettlementRegionMismatch, "Некорректный населённый пункт/регион.", "Указанный населённый пункт не принадлежит региону или не существует.");
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

	/// <summary>
	/// Отправляет OTP-код на указанный номер телефона.
	/// </summary>
	/// <remarks>
	/// Эндпоинт используется для подтверждения телефона при регистрации.
	/// </remarks>
	/// <param name="request">Тело запроса с номером телефона.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">OTP-код успешно отправлен.</response>
	/// <response code="400">Передан пустой или некорректный номер телефона.</response>
	/// <response code="500">Не удалось отправить OTP-код.</response>
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
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			throw;
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

	/// <summary>
	/// Инициирует процедуру сброса пароля по номеру телефона.
	/// </summary>
	/// <remarks>
	/// Если пользователь существует и его телефон подтверждён, сервер сохранит новый пароль во временное хранилище
	/// и отправит OTP-код для подтверждения операции.
	/// </remarks>
	/// <param name="request">Тело запроса с номером телефона и новым паролем.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Запрос на сброс пароля успешно создан.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="404">Пользователь с указанным номером телефона не найден.</response>
	/// <response code="409">Телефон пользователя ещё не подтверждён.</response>
	/// <response code="500">Произошла внутренняя ошибка или не удалось отправить OTP.</response>
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

	/// <summary>
	/// Подтверждает сброс пароля по номеру телефона и OTP-коду.
	/// </summary>
	/// <param name="request">Тело запроса с номером телефона и OTP-кодом.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Пароль успешно изменён.</response>
	/// <response code="400">Переданы некорректные данные запроса или неверный OTP-код.</response>
	/// <response code="404">Пользователь не найден.</response>
	/// <response code="409">Телефон не подтверждён или запрос на сброс пароля уже истёк.</response>
	/// <response code="500">Произошла внутренняя ошибка сервера.</response>
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

	/// <summary>
	/// Подтверждает номер телефона пользователя по OTP-коду.
	/// </summary>
	/// <param name="request">Тело запроса с номером телефона и OTP-кодом.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Номер телефона успешно подтверждён.</response>
	/// <response code="400">Переданы некорректные данные запроса или неверный OTP-код.</response>
	/// <response code="409">Телефон уже был подтверждён ранее.</response>
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

	/// <summary>
	/// Выполняет вход пользователя по номеру телефона и паролю.
	/// </summary>
	/// <param name="request">Тело запроса с номером телефона и паролем.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Аутентификация выполнена успешно.</response>
	/// <response code="401">Номер телефона не найден или пароль неверный.</response>
	/// <response code="409">Телефон найден, но ещё не подтверждён.</response>
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

	/// <summary>
	/// Возвращает публичные данные пользователя по номеру телефона.
	/// </summary>
	/// <param name="phoneNumber">Номер телефона пользователя.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Пользователь найден и успешно возвращён.</response>
	/// <response code="400">Передан номер телефона в некорректном формате.</response>
	/// <response code="404">Пользователь не найден.</response>
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

	/// <summary>
	/// Возвращает публичные данные пользователя по идентификатору.
	/// </summary>
	/// <param name="id">Идентификатор пользователя.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Пользователь найден и успешно возвращён.</response>
	/// <response code="404">Пользователь не найден.</response>
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

	/// <summary>
	/// Изменяет пароль пользователя.
	/// </summary>
	/// <remarks>
	/// Пользователь может изменить только собственный пароль. Старый пароль в теле запроса
	/// должен совпадать с паролем, переданным в заголовке <c>X-Actor-Password</c>.
	/// </remarks>
	/// <param name="id">Идентификатор пользователя, чей пароль изменяется.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Текущий пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="request">Тело запроса со старым и новым паролем.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Пароль успешно изменён.</response>
	/// <response code="400">Переданы некорректные данные запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
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

	/// <summary>
	/// Обновляет профиль пользователя.
	/// </summary>
	/// <remarks>
	/// Пользователь может изменять только собственный профиль. Тело запроса передаётся как
	/// <c>multipart/form-data</c>, чтобы можно было загрузить новый аватар.
	/// </remarks>
	/// <param name="id">Идентификатор пользователя, чей профиль обновляется.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="request">Обновлённые данные профиля пользователя.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Профиль успешно обновлён.</response>
	/// <response code="400">Переданы некорректные данные профиля или файла аватара.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	/// <response code="409">Обновление не удалось из-за конфликта данных.</response>
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
					SettlementName: request.SettlementName,
					AvatarImageId: avatarImageId),
				cancellationToken);

			if(!ok)
			{
				return this.ProblemWithCode(StatusCodes.Status404NotFound, ApiErrorCodes.UserNotFound, "Пользователь не найден.");
			}

			return NoContent();
		}
		catch(InvalidOperationException ex) when(IsSettlementMismatch(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.SettlementRegionMismatch, "Некорректный населённый пункт/регион.", "Указанный населённый пункт не принадлежит региону или не существует.");
		}
		catch(InvalidOperationException ex) when(IsGenderInvalid(ex))
		{
			return this.ProblemWithCode(StatusCodes.Status400BadRequest, ApiErrorCodes.GenderInvalid, "Некорректный пол.", "Допустимые значения: 'male', 'female' или null.");
		}
	}

	/// <summary>
	/// Изменяет номер телефона пользователя.
	/// </summary>
	/// <remarks>
	/// Пользователь может изменить только собственный номер телефона. Для подтверждения нового номера
	/// требуется OTP-код, ранее отправленный на этот номер.
	/// </remarks>
	/// <param name="id">Идентификатор пользователя, чей номер телефона изменяется.</param>
	/// <param name="actorUserId">Идентификатор пользователя, выполняющего операцию.</param>
	/// <param name="request">Тело запроса с новым номером телефона и OTP-кодом.</param>
	/// <param name="actorUserPassword">Пароль пользователя из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Номер телефона успешно изменён.</response>
	/// <response code="400">Переданы некорректные данные запроса или неверный OTP-код.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="404">Пользователь не найден.</response>
	/// <response code="409">Новый номер телефона уже занят.</response>
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

	/// <summary>
	/// Возвращает ленту пользователей с фильтрацией и опциональной пагинацией.
	/// </summary>
	/// <remarks>
	/// Поддерживается фильтрация по роли и географии. Формат ответа задаётся параметром
	/// <c>responseFormat</c>: список пользователей или только количество.
	/// </remarks>
	/// <param name="role">Опциональный фильтр по роли: <c>activist</c>, <c>coordinator</c> или <c>admin</c>.</param>
	/// <param name="regionName">Опциональный фильтр по региону.</param>
	/// <param name="settlementName">Опциональный фильтр по населённому пункту. Допустим только вместе с <c>regionName</c>.</param>
	/// <param name="responseFormat">Формат ответа: список пользователей или только количество.</param>
	/// <param name="start">Начальный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="end">Конечный индекс диапазона выборки, начиная с 1.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Лента пользователей успешно возвращена.</response>
	/// <response code="400">Переданы некорректные фильтры или параметры пагинации.</response>
	[HttpGet("feed")]
	[ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(UsersCountResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetFeed(
		[FromQuery] string? role,
		[FromQuery] string? regionName,
		[FromQuery] string? settlementName,
		[FromQuery] UserFeedResponseFormat responseFormat = UserFeedResponseFormat.Users,
		[FromQuery] int? start = null,
		[FromQuery] int? end = null,
		CancellationToken cancellationToken = default)
	{
		var invalid = TryBuildValidationProblemIfInvalidModel();
		if(invalid is not null)
		{
			return invalid;
		}

		var invalidFilters = TryBuildUsersFeedFilterValidationProblem(role, regionName, settlementName);
		if(invalidFilters is not null)
		{
			return invalidFilters;
		}

		var invalidPagination = TryBuildUsersPaginationValidationProblem(start, end);
		if(invalidPagination is not null)
		{
			return invalidPagination;
		}

		var normalizedResponseFormat = responseFormat switch
		{
			UserFeedResponseFormat.Count => UserResponseFormat.Count,
			_ => UserResponseFormat.Users,
		};

		UserRoleRules.TryNormalizeOptionalRole(role, out var normalizedRole, out _);

		var users = await _users.GetUsersAsync(normalizedRole, regionName, settlementName, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		if(string.Equals(normalizedResponseFormat, UserResponseFormat.Count, StringComparison.Ordinal))
		{
			return Ok(new UsersCountResponse(users.Count));
		}

		cancellationToken.ThrowIfCancellationRequested();
		var dtos = users.Select(ToDto).ToList();
		return Ok(ApplyUsersPagination(dtos, start, end));
	}

	/// <summary>
	/// Возвращает текущую роль пользователя.
	/// </summary>
	/// <param name="id">Идентификатор пользователя.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="200">Роль пользователя успешно возвращена.</response>
	/// <response code="404">Пользователь не найден.</response>
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

	/// <summary>
	/// Выдаёт пользователю роль координатора.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="id">Идентификатор пользователя, которому выдаётся роль координатора.</param>
	/// <param name="actorUserId">Идентификатор администратора, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль администратора из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Роль координатора успешно выдана.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Операция доступна только администратору.</response>
	/// <response code="404">Пользователь не найден.</response>
	/// <response code="409">Изменение роли запрещено правилами системы.</response>
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

	/// <summary>
	/// Забирает у пользователя роль координатора.
	/// </summary>
	/// <remarks>
	/// Операция доступна только пользователю с ролью <c>admin</c>.
	/// </remarks>
	/// <param name="id">Идентификатор пользователя, у которого забирается роль координатора.</param>
	/// <param name="actorUserId">Идентификатор администратора, выполняющего операцию.</param>
	/// <param name="actorUserPassword">Пароль администратора из заголовка <c>X-Actor-Password</c>.</param>
	/// <param name="cancellationToken">Токен отмены HTTP-запроса.</param>
	/// <response code="204">Роль координатора успешно отозвана.</response>
	/// <response code="400">Переданы некорректные параметры запроса.</response>
	/// <response code="401">Указаны неверные учётные данные пользователя.</response>
	/// <response code="403">Операция доступна только администратору.</response>
	/// <response code="404">Пользователь не найден.</response>
	/// <response code="409">Изменение роли запрещено правилами системы.</response>
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
			u.SettlementName,
			u.Role,
			u.IsPhoneConfirmed)
	{
		AvatarImageUrl = u.AvatarImageUrl,
	};

	private ActionResult? TryBuildUsersFeedFilterValidationProblem(string? role, string? regionName, string? settlementName)
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

		if(settlementName is not null)
		{
			var list = new List<string>();

			if(string.IsNullOrWhiteSpace(settlementName))
			{
				list.Add("SettlementName must not be empty.");
			}

			if(string.IsNullOrWhiteSpace(regionName))
			{
				list.Add("settlementName can be used only together with regionName.");
			}

			if(list.Count > 0)
			{
				errors["settlementName"] = list.ToArray();
			}
		}

		return errors.Count == 0 ? null : this.ValidationProblemWithCode(ApiErrorCodes.ValidationFailed, errors, title: "Некорректный запрос.", detail: $"Проверьте параметры role/regionName/settlementName. role допускает только '{UserRoles.Activist}', '{UserRoles.Coordinator}' или '{UserRoles.Admin}', settlementName допускается только вместе с regionName.");
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

	private IActionResult? TryBuildValidationProblemIfInvalidModel()
	{
		if(ModelState.IsValid)
		{
			return null;
		}

		var errors = ModelState
			.Where(x => x.Value?.Errors.Count > 0)
			.ToDictionary(
				x => x.Key,
				x => x.Value!.Errors
					.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
					.ToArray());

		return this.ValidationProblemWithCode(
			ApiErrorCodes.ValidationFailed,
			errors,
			title: "Некорректный запрос.",
			detail: "Проверьте query-параметры и их допустимые значения.");
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

	/// <summary>
	/// Модель <c>multipart/form-data</c> для регистрации пользователя.
	/// </summary>
	public sealed class RegisterUserFormRequest
	{
		/// <summary>
		/// Фамилия пользователя.
		/// </summary>
		public string LastName { get; set; } = string.Empty;

		/// <summary>
		/// Имя пользователя.
		/// </summary>
		public string FirstName { get; set; } = string.Empty;

		/// <summary>
		/// Отчество пользователя.
		/// </summary>
		public string? MiddleName { get; set; }

		/// <summary>
		/// Пол пользователя. Допустимые значения нормализуются сервером к <c>male</c> или <c>female</c>.
		/// </summary>
		public string? Gender { get; set; }

		/// <summary>
		/// Номер телефона пользователя.
		/// </summary>
		public string PhoneNumber { get; set; } = string.Empty;

		/// <summary>
		/// Пароль пользователя.
		/// </summary>
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// Дата рождения пользователя.
		/// </summary>
		public DateOnly BirthDate { get; set; }

		/// <summary>
		/// Название региона проживания пользователя.
		/// </summary>
		public string RegionName { get; set; } = string.Empty;

		/// <summary>
		/// Название населённого пункта проживания пользователя.
		/// </summary>
		public string SettlementName { get; set; } = string.Empty;

		/// <summary>
		/// Файл аватара пользователя.
		/// </summary>
		public IFormFile? AvatarImage { get; set; }
	}

	/// <summary>
	/// Модель <c>multipart/form-data</c> для обновления профиля пользователя.
	/// </summary>
	public sealed class UpdateUserFormRequest
	{
		/// <summary>
		/// Фамилия пользователя.
		/// </summary>
		public string LastName { get; set; } = string.Empty;

		/// <summary>
		/// Имя пользователя.
		/// </summary>
		public string FirstName { get; set; } = string.Empty;

		/// <summary>
		/// Отчество пользователя.
		/// </summary>
		public string? MiddleName { get; set; }

		/// <summary>
		/// Пол пользователя. Допустимые значения нормализуются сервером к <c>male</c> или <c>female</c>.
		/// </summary>
		public string? Gender { get; set; }

		/// <summary>
		/// Дата рождения пользователя.
		/// </summary>
		public DateOnly BirthDate { get; set; }

		/// <summary>
		/// Название региона проживания пользователя.
		/// </summary>
		public string RegionName { get; set; } = string.Empty;

		/// <summary>
		/// Название населённого пункта проживания пользователя.
		/// </summary>
		public string SettlementName { get; set; } = string.Empty;

		/// <summary>
		/// Новый файл аватара пользователя.
		/// </summary>
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
		if(string.IsNullOrWhiteSpace(r.SettlementName)) errors[nameof(r.SettlementName)] = new[] { "SettlementName is required." };

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
		if(string.IsNullOrWhiteSpace(r.SettlementName)) errors[nameof(r.SettlementName)] = new[] { "SettlementName is required." };

		return errors;
	}

	private static bool IsPhoneDuplicate(InvalidOperationException ex) =>
		ex.Message.Contains("PhoneNumber already exists", StringComparison.OrdinalIgnoreCase);

	private static bool IsSettlementMismatch(InvalidOperationException ex) =>
		ex.Message.Contains("Settlement does not belong", StringComparison.OrdinalIgnoreCase);

	private static bool IsGenderInvalid(InvalidOperationException ex) =>
		ex.Message.Contains("Gender is invalid", StringComparison.OrdinalIgnoreCase);
}