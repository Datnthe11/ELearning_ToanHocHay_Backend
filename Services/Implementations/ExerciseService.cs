using AutoMapper;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Exercise;
using ELearning_ToanHocHay_Control.Models.DTOs.Question;
using ELearning_ToanHocHay_Control.Repositories.Implementations;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class ExerciseService : IExerciseService
    {
        private readonly IExerciseRepository _exerciseRepository;
        private readonly IExerciseAttemptRepository _exerciseAttemptRepository;
        private readonly IMapper _mapper;

        public ExerciseService(IExerciseRepository exerciseRepository, IExerciseAttemptRepository exerciseAttemptRepository, IMapper mapper)
        {
            _exerciseRepository = exerciseRepository;
            _exerciseAttemptRepository = exerciseAttemptRepository;
            _mapper = mapper;
        }

        public async Task<ApiResponse<bool>> AddQuestionsToExerciseAsync(int exerciseId, AddQuestionsToExerciseDto dto)
        {
            var exercise = await _exerciseRepository.GetExerciseByIdAsync(exerciseId);
            if (exercise == null)
            {
                return ApiResponse<bool>.ErrorResponse(
                    "Exercise not found",
                    new List<string> { $"ExerciseId {exerciseId} not found" }
                );
            }

            if (!dto.QuestionIds.Any())
            {
                return ApiResponse<bool>.ErrorResponse(
                    "QuestionIds is empty"
                );
            }

            var scorePerQuestion = dto.ScorePerQuestion
                ?? (exercise.TotalScores / exercise.TotalQuestions);

            var success = await _exerciseRepository.AddQuestionsToExerciseAsync(
                exerciseId,
                dto.QuestionIds,
                scorePerQuestion
            );

            return success
                ? ApiResponse<bool>.SuccessResponse(true, "Questions added successfully")
                : ApiResponse<bool>.ErrorResponse("Failed to add questions");
        }

        public async Task<ApiResponse<ExerciseDto>> CreateExerciseAsync(ExerciseRequestDto exercise)
        {
            try
            {
                var _exercise = new Exercise
                {
                    TopicId = exercise.TopicId,
                    ChapterId = exercise.ChapterId,
                    ExerciseName = exercise.ExerciseName,
                    ExerciseType = exercise.ExerciseType,
                    TotalQuestions = exercise.TotalQuestions,
                    DurationMinutes = exercise.DurationMinutes,
                    IsFree = exercise.IsFree,
                    IsActive = exercise.IsActive,
                    TotalScores = exercise.TotalScores,
                    PassingScore = exercise.PassingScore,
                    Status = exercise.Status,
                    CreatedBy = 3, // Use UserId in session for CreatedBy
                    CreatedAt = DateTime.UtcNow,
                };
                await _exerciseRepository.CreateExerciseAsync(_exercise);
                return ApiResponse<ExerciseDto>.SuccessResponse(
                    _mapper.Map<ExerciseDto>(_exercise),
                    "Exercise created successfully"
                    );
            }
            catch (Exception ex)
            {
                return ApiResponse<ExerciseDto>.ErrorResponse(
                    "Error creating exercise",
                    new List<string> { ex.Message }
                    );
            }
        }

        public async Task<ApiResponse<bool>> DeleteExerciseAsync(int exerciseId)
        {
            try
            {
                var exercise = await _exerciseRepository.GetExerciseByIdAsync(exerciseId);
                if (exercise == null)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Exercise not found",
                        new List<string>
                        {
                            $"No exercise found with id: {exerciseId}"
                        }
                    );
                }

                var hasAttempt = await _exerciseAttemptRepository
                    .ExistsByExerciseIdAsync(exerciseId);

                if (hasAttempt)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "EXERCISE_HAS_ATTEMPTS",
                        new List<string>
                        {
                            "Cannot delete exercise that has attempts"
                        }
                    );
                }
                var deleted = await _exerciseRepository.DeleteExerciseAsync(exerciseId);
                return ApiResponse<bool>.SuccessResponse(deleted,
                    "Exercise deleted successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse("Error deleting exercise",
                    new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<IEnumerable<ExerciseDto>>> GetAllAsync()
        {
            try
            {
                var exercises = await _exerciseRepository.GetAllAsync();
                var activeExercises = exercises.Where(e => e.IsActive == true);
                return ApiResponse<IEnumerable<ExerciseDto>>.SuccessResponse(_mapper.Map<IEnumerable<ExerciseDto>>(activeExercises), "Exercises retrieved successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<IEnumerable<ExerciseDto>>.ErrorResponse(
                    "Error retrieving exercises",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<IEnumerable<ExerciseDto>>> GetByChapterIdAsync(int chapterId)
        {
            var exercises = await _exerciseRepository.GetByChapterIdAsync(chapterId);
            var activeExercises = exercises.Where(e => e.IsActive == true);
            return ApiResponse<IEnumerable<ExerciseDto>>
                .SuccessResponse(_mapper.Map<IEnumerable<ExerciseDto>>(activeExercises));
        }

        // Trong ExerciseService.cs của Backend
        public async Task<ApiResponse<ExerciseDetailDto>> GetByIdAsync(int exerciseId)
        {
            // 1. Lấy thực thể kèm theo Questions và Options
            var exercise = await _exerciseRepository.GetExerciseWithQuestionsAsync(exerciseId);
            if (exercise == null) return ApiResponse<ExerciseDetailDto>.ErrorResponse("Không tìm thấy");

            // 2. Map các thông tin cơ bản (Id, Name, Duration)
            var dto = _mapper.Map<ExerciseDetailDto>(exercise);

            // 3. Đổ dữ liệu Questions
            if (exercise.ExerciseQuestions != null)
            {
                dto.Questions = exercise.ExerciseQuestions
                    .Where(eq => eq.Question != null) // Đảm bảo Question không null
                    .Select(eq => new QuestionDto
                    {
                        QuestionId = eq.QuestionId,
                        QuestionText = eq.Question.QuestionText,
                        QuestionType = eq.Question.QuestionType,
                        DifficultyLevel = eq.Question.DifficultyLevel,
                        Options = eq.Question.QuestionOptions?.Select(opt => new QuestionOptionDto
                        {
                            OptionId = opt.OptionId,
                            OptionText = opt.OptionText,
                            IsCorrect = opt.IsCorrect
                        }).ToList() ?? new List<QuestionOptionDto>()
                    }).ToList();
            }

            return ApiResponse<ExerciseDetailDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<IEnumerable<ExerciseDto>>> GetByLessonIdAsync(int lessonId)
        {
            var exercises = await _exerciseRepository.GetByLessonIdAsync(lessonId);
            var activeExercises = exercises.Where(e => e.IsActive == true);
            return ApiResponse<IEnumerable<ExerciseDto>>
                .SuccessResponse(_mapper.Map<IEnumerable<ExerciseDto>>(activeExercises));
        }

        public async Task<ApiResponse<IEnumerable<ExerciseDto>>> GetByTopicIdAsync(int topicId)
        {
            var exercises = await _exerciseRepository.GetByTopicIdAsync(topicId);
            var activeExercises = exercises.Where(e => e.IsActive == true);
            return ApiResponse<IEnumerable<ExerciseDto>>
                .SuccessResponse(_mapper.Map<IEnumerable<ExerciseDto>>(activeExercises));
        }
        /*public async Task<ApiResponse<IEnumerable<ExerciseQuestionDto>>> GetExerciseQuestionsAsync(int exerciseId)
        {
            var questions = await _exerciseRepository.GetExerciseQuestionsAsync(exerciseId);
            return ApiResponse<IEnumerable<ExerciseQuestionDto>>
                .SuccessResponse(_mapper.Map<IEnumerable<ExerciseQuestionDto>>(questions));
        }*/

        public async Task<ApiResponse<bool>> RemoveQuestionFromExerciseAsync(int exerciseId, int questionId)
        {
            var isExist = await _exerciseRepository.GetExerciseByIdAsync(exerciseId);
            if (isExist == null)
                return ApiResponse<bool>.ErrorResponse("Exercise not found");

            var success = await _exerciseRepository
                .RemoveQuestionFromExerciseAsync(exerciseId, questionId);

            return success
                ? ApiResponse<bool>.SuccessResponse(true, "Question removed")
                : ApiResponse<bool>.ErrorResponse("Question not found");
        }

        public async Task<ApiResponse<ExerciseDto>> UpdateExerciseAsync(int id, ExerciseRequestDto exerciseRequestDto)
        {
            try
            {
                var exercise = await _exerciseRepository.GetExerciseByIdAsync(id);
                if (exercise == null)
                    return ApiResponse<ExerciseDto>.ErrorResponse(
                            "ExerciseId not found",
                            new List<string> { $"No user found with ID: {id}" }
                        );
                // Update info
                exercise.TopicId = exerciseRequestDto.TopicId;
                exercise.ChapterId = exerciseRequestDto.ChapterId;
                exercise.ExerciseName = exerciseRequestDto.ExerciseName;
                exercise.ExerciseType = exerciseRequestDto.ExerciseType;
                exercise.TotalQuestions = exerciseRequestDto.TotalQuestions;
                exercise.DurationMinutes = exerciseRequestDto.DurationMinutes;
                exercise.IsFree = exerciseRequestDto.IsFree;
                exercise.IsActive = exerciseRequestDto.IsActive;
                exercise.TotalScores = exerciseRequestDto.TotalScores;
                exercise.PassingScore = exerciseRequestDto.PassingScore;
                exercise.Status = exerciseRequestDto.Status;
                await _exerciseRepository.UpdateExerciseAsync(exercise);

                return ApiResponse<ExerciseDto>.SuccessResponse(_mapper.Map<ExerciseDto>(exercise), "Exercise updated successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExerciseDto>.ErrorResponse(
                    "Error updating exercise",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<bool>> UpdateExerciseQuestionScoreAsync(int exerciseId, int questionId, double score)
        {
            var success = await _exerciseRepository
                .UpdateExerciseQuestionScoreAsync(exerciseId, questionId, score);

            return success
                ? ApiResponse<bool>.SuccessResponse(true, "Score updated")
                : ApiResponse<bool>.ErrorResponse("Question not found");
        }
    }
}
