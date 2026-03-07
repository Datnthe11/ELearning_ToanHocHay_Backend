import google.generativeai as genai
import json
from typing import Dict, List, Optional
import os
from dotenv import load_dotenv
import sys
import requests
from PIL import Image
from io import BytesIO
import logging

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Add parent directory to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)
if parent_dir not in sys.path:
    sys.path.append(parent_dir)

# Import shared config manager
try:
    from Config_manager import api_key_manager
except ImportError:
    # Fallback if running from root
    sys.path.append(current_dir)
    from Config_manager import api_key_manager

from Prompts import hint_prompt, feedback_prompt

# Load environment variables
load_dotenv()

# Configure API key on load
api_key_manager.configure()

class GeminiAIService:
    """Service to interact with Google Gemini AI for educational hints and feedback"""
    
    def __init__(self, model_name: str = "gemini-2.5-flash"):
        # Initialize model with JSON mode for structured responses
        self.model_name = model_name
        self.model = genai.GenerativeModel(
            model_name,
            generation_config=genai.types.GenerationConfig(
                response_mime_type="application/json"
            )
        )
    
    # ==================== HINT GENERATION ====================
    def generate_hint(
        self,
        question_text: str,
        question_type: str,
        difficulty_level: str,
        student_answer: str,
        hint_level: int = 1,
        options: Optional[List[Dict]] = None,
        question_id: int = None,
        question_image_url: Optional[str] = None
    ) -> Dict[str, str]:
        """
        Generate a hint for a specific question without revealing the answer.
        """
        try:
            # Format options text
            options_text = self._format_options(options, include_correct=False)
            
            # Format the prompt
            formatted_prompt = hint_prompt.format(
                hint_level=hint_level,
                question_text=question_text,
                question_type=question_type,
                difficulty_level=difficulty_level,
                student_answer=student_answer,
                options_text=options_text
            )
            
            # Add JSON format instruction
            formatted_prompt += "\n\nCung cấp phản hồi dưới dạng JSON với cấu trúc: {\"hint_text\": \"...\", \"hint_level\": 1}"
            
            # Prepare content with image if provided
            content = self._prepare_content_with_image(formatted_prompt, question_image_url)
            
            # Call Gemini API with retry logic
            response = self._call_api_with_retry(content)
            
            if response.get("Status") == "error":
                error_type = response.get("ErrorType", "unknown")
                
                if error_type == "quota":
                    # Quota exhausted - specific message
                    return {
                        "hint_text": "🚫 Hệ thống AI đã hết quota. Vui lòng liên hệ quản trị viên để nạp thêm quota.",
                        "hint_level": hint_level,
                        "question_id": question_id,
                        "status": "error",
                        "error": "QUOTA_EXHAUSTED",
                        "error_type": "quota"
                    }
                else:
                    # Other API errors
                    return {
                        "hint_text": "AI đang gặp sự cố khi tạo gợi ý. Vui lòng thử lại.",
                        "hint_level": hint_level,
                        "question_id": question_id,
                        "status": "error",
                        "error": response.get("Error", "Unknown error"),
                        "error_type": error_type
                    }
            
            # Parse JSON response
            response_text = response.get("text", "")
            logger.info(f"Raw AI Hint Response: {response_text}") # Log raw response
            
            # Check if response is empty
            if not response_text or not response_text.strip():
                logger.error("Empty response from Gemini API")
                return {
                    "hint_text": "AI trả về phản hồi rỗng. Vui lòng thử lại.",
                    "hint_level": hint_level,
                    "question_id": question_id,
                    "status": "error",
                    "error": "Empty response from API"
                }
            
            cleaned_text = self._clean_json_text(response_text)
            hint_data = json.loads(cleaned_text)
            
            return {
                "hint_text": hint_data.get("hint_text", response_text),
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "success"
            }
        
        except json.JSONDecodeError as e:
            # response_text is already defined in the try block above
            logger.error(f"JSON decode error: {str(e)} - Raw text: {response_text if 'response_text' in locals() else 'N/A'}")
            return {
                "hint_text": "AI đang gặp sự cố khi tạo gợi ý. Vui lòng thử lại.",
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "error",
                "error": str(e)
            }
        
        except Exception as e:
            logger.error(f"Error generating hint: {str(e)}")
            return {
                "hint_text": f"Lỗi tạo gợi ý: {str(e)}",
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "error",
                "error": str(e)
            }
    
    # ==================== FEEDBACK GENERATION ====================
    def generate_feedback(
        self,
        question_text: str,
        question_type: str,
        student_answer: str,
        correct_answer: str,
        is_correct: bool,
        explanation: Optional[str] = None,
        options: Optional[List[Dict]] = None,
        attempt_id: int = None,
        question_image_url: Optional[str] = None
    ) -> Dict[str, str]:
        """
        Generate comprehensive feedback after exercise completion.
        """
        try:
            # Format options text (including correct answers for feedback)
            options_text = self._format_options(options, include_correct=True)
            
            from Prompts import feedback_prompt
            
            # Format the prompt
            formatted_prompt = feedback_prompt.format(
                question_text=question_text,
                question_type=question_type,
                correct_answer=correct_answer,
                explanation=explanation or "Không có giải thích thêm",
                student_answer=student_answer,
                is_correct="✓ Đúng" if is_correct else "✗ Sai",
                options_text=options_text
            )
            
            # Add JSON format instruction
            formatted_prompt += """\n\nCung cấp phản hồi dưới dạng JSON với cấu trúc:
{
    "full_solution": "Lời giải hoàn chỉnh...",
    "mistake_analysis": "Phân tích lỗi...",
    "improvement_advice": "Lời khuyên cải thiện..."
}"""
            
            # Prepare content with image if provided
            content = self._prepare_content_with_image(formatted_prompt, question_image_url)
            
            # Call Gemini API with retry logic
            response = self._call_api_with_retry(content)
            
            if response.get("Status") == "error":
                error_type = response.get("ErrorType", "unknown")
                
                if error_type == "quota":
                    # Quota exhausted - specific message
                    return {
                        "full_solution": "🚫 Hệ thống AI đã hết quota. Vui lòng liên hệ quản trị viên để nạp thêm quota.",
                        "mistake_analysis": "",
                        "improvement_advice": "",
                        "attempt_id": attempt_id,
                        "status": "error",
                        "error": "QUOTA_EXHAUSTED",
                        "error_type": "quota"
                    }
                else:
                    # Other API errors
                    return {
                        "full_solution": "AI đang gặp sự cố khi tạo phản hồi. Vui lòng thử lại.",
                        "mistake_analysis": "",
                        "improvement_advice": "",
                        "attempt_id": attempt_id,
                        "status": "error",
                        "error": response.get("Error", "Unknown error"),
                        "error_type": error_type
                    }
            
            # Parse JSON response
            response_text = response.get("text", "")
            logger.info(f"Raw AI Feedback Response: {response_text}") # Log raw response
            
            # Check if response is empty
            if not response_text or not response_text.strip():
                logger.error("Empty response from Gemini API")
                return {
                    "full_solution": "AI trả về phản hồi rỗng. Vui lòng thử lại.",
                    "mistake_analysis": "",
                    "improvement_advice": "",
                    "attempt_id": attempt_id,
                    "status": "error",
                    "error": "Empty response from API"
                }
            
            cleaned_text = self._clean_json_text(response_text)
            feedback_data = json.loads(cleaned_text)
            
            return {
                "full_solution": feedback_data.get("full_solution", ""),
                "mistake_analysis": feedback_data.get("mistake_analysis", ""),
                "improvement_advice": feedback_data.get("improvement_advice", ""),
                "attempt_id": attempt_id,
                "status": "success"
            }
        
        except json.JSONDecodeError as e:
            logger.error(f"JSON decode error: {str(e)} - Raw text: {response_text if 'response_text' in locals() else 'N/A'}")
            return {
                "full_solution": "AI đang gặp sự cố khi tạo phản hồi. Vui lòng thử lại.",
                "mistake_analysis": "",
                "improvement_advice": "",
                "attempt_id": attempt_id,
                "status": "error",
                "error": str(e)
            }
        
        except Exception as e:
            logger.error(f"Error generating feedback: {str(e)}")
            return {
                "full_solution": f"Lỗi tạo feedback: {str(e)}",
                "mistake_analysis": "",
                "improvement_advice": "",
                "attempt_id": attempt_id,
                "status": "error",
                "error": str(e)
            }
    
    # ==================== INSIGHT GENERATION ====================
    def generate_insight(
        self,
        question_text: str,
        student_answer: str,
        correct_answer: str
    ) -> Dict[str, Any]:
        """
        Generate educational insights based on student answers to identify weaknesses.
        """
        try:
            from Prompts import general_improvement_prompt
            
            # Format the prompt
            formatted_prompt = general_improvement_prompt.format(
                question_text=question_text,
                student_answer=student_answer,
                correct_answer=correct_answer
            )
            
            # Add JSON format instruction
            formatted_prompt += """\n\nCung cấp phản hồi dưới dạng JSON với cấu trúc:
{
    "concepts_to_review": ["Khái niệm 1", "Khái niệm 2"],
    "recommended_exercises": ["Bài tập 1", "Bài tập 2"],
    "quick_tips": ["Mẹo 1", "Mẹo 2"],
    "summary": "Tóm tắt ngắn gọn nhận xét cho em..."
}"""
            
            # Call Gemini API with retry logic
            response = self._call_api_with_retry([formatted_prompt])
            
            if response.get("Status") == "error":
                return {
                    "status": "error",
                    "error": response.get("Error", "Unknown error")
                }
            
            # Parse JSON response
            response_text = response.get("text", "")
            cleaned_text = self._clean_json_text(response_text)
            insight_data = json.loads(cleaned_text)
            
            return {
                "concepts_to_review": insight_data.get("concepts_to_review", []),
                "recommended_exercises": insight_data.get("recommended_exercises", []),
                "quick_tips": insight_data.get("quick_tips", []),
                "summary": insight_data.get("summary", ""),
                "status": "success"
            }
            
        except Exception as e:
            logger.error(f"Error generating insight: {str(e)}")
            return {
                "status": "error",
                "error": str(e)
            }

    
    def _clean_json_text(self, text: str) -> str:
        """Clean JSON text from Markdown formatting"""
        if not text:
            return ""
        
        text = text.strip()
        # Remove markdown code blocks if present
        if text.startswith("```"):
            # Find the first newline to skip the language identifier (e.g. ```json)
            first_newline = text.find("\n")
            if first_newline != -1:
                text = text[first_newline+1:]
            else:
                # If no newline, just strip the first 3 chars
                 text = text[3:]
            
            # Remove the last ```
            if text.endswith("```"):
                text = text[:-3]
                
        return text.strip()
    
    # ==================== HELPER METHODS ====================
    def _download_image(self, image_url: str) -> Optional[Image.Image]:
        """Download image from URL and convert to PIL Image"""
        try:
            if not image_url or not image_url.startswith(('http://', 'https://')):
                return None
            
            response = requests.get(image_url, timeout=10)
            response.raise_for_status()
            
            image = Image.open(BytesIO(response.content))
            return image
        
        except Exception as e:
            logger.warning(f"Failed to download image from {image_url}: {str(e)}")
            return None
    
    def _prepare_content_with_image(
        self,
        prompt_text: str,
        image_url: Optional[str] = None
    ) -> List:
        """Prepare content for Gemini with optional image"""
        content = [prompt_text]
        
        if image_url:
            image = self._download_image(image_url)
            if image:
                content.append(image)
                logger.info(f"Added image to prompt: {image_url}")
        
        return content
    
    def _call_api_with_retry(self, content) -> Dict:
        """Call Gemini API with retry logic on key rotation"""
        max_retries = len(api_key_manager.api_keys)
        last_error = None
        quota_errors = 0
        
        for attempt in range(max_retries):
            try:
                response = self.model.generate_content(content)
                return {
                    "text": response.text.strip(),
                    "Status": "success"
                }
            
            except Exception as e:
                error_str = str(e).lower()
                last_error = str(e)
                
                # Check if this is a quota/rate limit error
                is_quota_error = any(keyword in error_str for keyword in [
                    'quota', 'resource_exhausted', 'resourceexhausted', 
                    '429', 'rate limit', 'ratelimit'
                ])
                
                if is_quota_error:
                    quota_errors += 1
                    logger.error(f"⚠️ API key #{attempt + 1} HẾT QUOTA: {str(e)}")
                else:
                    logger.error(f"API call attempt {attempt + 1} failed: {str(e)}")
                
                if attempt < max_retries - 1:
                    # Rotate API key and reconfigure
                    new_key = api_key_manager.rotate_key()
                    api_key_manager.configure()
                    # Recreate model with new API key
                    self.model = genai.GenerativeModel(
                        self.model_name,
                        generation_config=genai.types.GenerationConfig(
                            response_mime_type="application/json"
                        )
                    )
                else:
                    # All retries exhausted
                    if quota_errors == max_retries:
                        # All keys hit quota limit
                        return {
                            "text": f"🚫 TẤT CẢ {max_retries} API KEY ĐỀU ĐÃ HẾT QUOTA",
                            "Status": "error",
                            "Error": "QUOTA_EXHAUSTED",
                            "ErrorType": "quota",
                            "Details": last_error
                        }
                    else:
                        # Other errors
                        return {
                            "text": f"API Error after {max_retries} attempts: {last_error}",
                            "Status": "error",
                            "Error": last_error,
                            "ErrorType": "api_error"
                        }
        
        return {
            "text": "Unknown error",
            "Status": "error",
            "Error": "Max retries exceeded",
            "ErrorType": "unknown"
        }
    
    def _format_options(
        self,
        options: Optional[List[Dict]],
        include_correct: bool = False
    ) -> str:
        """Format options for display in prompts"""
        if not options:
            return "Không có lựa chọn"
        
        options_text = ""
        for idx, opt in enumerate(options, 1):
            option_line = f"{idx}. {opt.get('OptionText', '')}"
            
            if include_correct:
                is_correct = opt.get('IsCorrect', False)
                option_line += " ✓" if is_correct else ""
            
            if opt.get('ImageUrl'):
                option_line += f" [Hình ảnh: {opt['ImageUrl']}]"
            
            options_text += option_line + "\n"
        
        return options_text


# ==================== STANDALONE FUNCTIONS ====================
async def get_ai_hint(
    question_data: Dict,
    student_answer: str,
    hint_level: int = 1
) -> Dict[str, str]:
    """
    Standalone function to get AI hint.
    
    Expected question_data format:
    {
        "QuestionId": 1,
        "QuestionText": "...",
        "QuestionType": "MultipleChoice",
        "DifficultyLevel": "Medium",
        "QuestionImageUrl": "https://...", (optional)
        "QuestionOptions": [
            {"OptionId": 1, "OptionText": "..."},
            ...
        ]
    }
    """
    service = GeminiAIService()
    
    return service.generate_hint(
        question_text=question_data.get("QuestionText"),
        question_type=question_data.get("QuestionType"),
        difficulty_level=question_data.get("DifficultyLevel"),
        student_answer=student_answer,
        hint_level=hint_level,
        options=question_data.get("QuestionOptions"),
        question_id=question_data.get("QuestionId"),
        question_image_url=question_data.get("QuestionImageUrl")
    )


async def get_ai_feedback(
    question_data: Dict,
    student_answer_data: Dict,
    correct_answer: str,
    is_correct: bool
) -> Dict[str, str]:
    """
    Standalone function to get AI feedback.
    
    Expected question_data format:
    {
        "QuestionId": 1,
        "QuestionText": "...",
        "QuestionType": "MultipleChoice",
        "Explanation": "...",
        "QuestionImageUrl": "https://...", (optional)
        "QuestionOptions": [
            {"OptionId": 1, "OptionText": "...", "IsCorrect": true/false},
            ...
        ]
    }
    
    Expected student_answer_data format:
    {
        "AttemptId": 1,
        "AnswerText": "...",
        "SelectedOptionId": 1
    }
    """
    service = GeminiAIService()
    
    return service.generate_feedback(
        question_text=question_data.get("QuestionText"),
        question_type=question_data.get("QuestionType"),
        student_answer=student_answer_data.get("AnswerText"),
        correct_answer=correct_answer,
        is_correct=is_correct,
        explanation=question_data.get("Explanation"),
        options=question_data.get("QuestionOptions"),
        attempt_id=student_answer_data.get("AttemptId"),
        question_image_url=question_data.get("QuestionImageUrl")
    )


if __name__ == "__main__":
    # Test example
    service = GeminiAIService()
    
    # Test hint
    print("Testing hint generation...")
    hint_result = service.generate_hint(
        question_text="Tính đạo hàm của f(x) = 2x³ + 3x²",
        question_type="FillBlank",
        difficulty_level="Medium",
        student_answer="f'(x) = 6x² + 6x",
        hint_level=1
    )
    print(f"Hint: {hint_result}")
    
    print("\n" + "="*50 + "\n")
    
    # Test feedback
    print("Testing feedback generation...")
    feedback_result = service.generate_feedback(
        question_text="Tính đạo hàm của f(x) = 2x³ + 3x²",
        question_type="FillBlank",
        student_answer="f'(x) = 6x² + 6x",
        correct_answer="f'(x) = 6x² + 6x",
        is_correct=True,
        explanation="Sử dụng quy tắc lũy thừa để tính đạo hàm"
    )
    print(f"Feedback: {json.dumps(feedback_result, ensure_ascii=False, indent=2)}")
