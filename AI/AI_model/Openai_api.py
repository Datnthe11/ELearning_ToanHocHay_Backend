import openai
import json
from typing import Dict, List, Optional, Any
import os
# Clean up proxy environment variables that cause issues with OpenAI v1.x
for key in ["HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy"]:
    if key in os.environ:
        os.environ.pop(key)
from dotenv import load_dotenv
import sys
import requests
from PIL import Image
from io import BytesIO
import logging
import base64

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from Prompts import hint_prompt, feedback_prompt, ai_assessment_prompt, ai_roadmap_prompt

# Load environment variables
load_dotenv()

# ==================== API KEY ROTATION ====================
class OpenAIAPIKeyManager:
    """Manage multiple OpenAI API keys with rotation on failure"""
    
    def __init__(self):
        # Get API keys from env (support multiple keys: OPENAI_API_KEY_1, OPENAI_API_KEY_2, etc.)
        self.api_keys = []
        self.current_index = 0
        
        # Try to get multiple API keys
        i = 1
        while True:
            key = os.getenv(f"OPENAI_API_KEY_{i}")
            if not key:
                break
            self.api_keys.append(key)
            i += 1
        
        # If no numbered keys, try single key
        if not self.api_keys:
            key = os.getenv("OPENAI_API_KEY")
            if key:
                self.api_keys.append(key)
        
        if not self.api_keys:
            raise ValueError("No OpenAI API keys found in environment variables")
        
        logger.info(f"Loaded {len(self.api_keys)} OpenAI API key(s)")
    
    def get_current_key(self) -> str:
        """Get current API key"""
        return self.api_keys[self.current_index]
    
    def rotate_key(self) -> str:
        """Rotate to next API key on failure"""
        self.current_index = (self.current_index + 1) % len(self.api_keys)
        logger.warning(f"Rotating to API key #{self.current_index + 1}")
        return self.get_current_key()


# Initialize API key manager
api_key_manager = OpenAIAPIKeyManager()
# Global config for v1.x (falls back to default client)
openai.api_key = api_key_manager.get_current_key()


class OpenAIService:
    """Service to interact with OpenAI API for educational hints and feedback"""
    
    def __init__(self, model_name: str = "gpt-4o-mini"):
        self.model_name = model_name
    
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
        
        Args:
            question_text: The question text
            question_type: Type of question (MultipleChoice, TrueFalse, FillBlank, Essay)
            difficulty_level: Difficulty level (Easy, Medium, Hard)
            student_answer: Student's current answer or "Chưa trả lời"
            hint_level: Hint level 1-3 (1=general, 2=specific, 3=step-by-step)
            options: List of options for multiple choice (without IsCorrect field)
            question_id: Question ID for tracking
            question_image_url: URL to question image (optional)
        
        Returns:
            Dictionary with HintText and HintLevel
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
            
            # Prepare messages with optional image
            messages = self._prepare_messages_with_image(formatted_prompt, question_image_url)
            
            # Call OpenAI API with retry logic
            response = self._call_api_with_retry(messages)
            
            if response.get("Status") == "error":
                return response
            
            # Parse JSON response
            response_text = response.get("text", "")
            hint_data = json.loads(response_text)
            
            return {
                "hint_text": hint_data.get("hint_text", response_text),
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "success"
            }
        
        except json.JSONDecodeError as e:
            logger.error(f"JSON decode error: {str(e)}")
            return {
                "hint_text": f"Lỗi xử lý phản hồi AI: {str(e)}",
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
        
        Args:
            question_text: The question text
            question_type: Type of question
            student_answer: Student's answer
            correct_answer: Correct answer
            is_correct: Whether student answer is correct
            explanation: Teacher's explanation (optional)
            options: List of options for multiple choice (WITH IsCorrect field)
            attempt_id: Attempt ID for tracking
            question_image_url: URL to question image (optional)
        
        Returns:
            Dictionary with FullSolution, MistakeAnalysis, ImprovementAdvice
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
            
            # Prepare messages with optional image
            messages = self._prepare_messages_with_image(formatted_prompt, question_image_url)
            
            # Call OpenAI API with retry logic
            response = self._call_api_with_retry(messages)
            
            if response.get("Status") == "error":
                return {
                    "full_solution": response.get("text", "Lỗi tạo feedback"),
                    "mistake_analysis": "",
                    "improvement_advice": "",
                    "attempt_id": attempt_id,
                    "status": "error",
                    "error": response.get("Error")
                }
            
            # Parse JSON response
            response_text = response.get("text", "")
            feedback_data = json.loads(response_text)
            
            return {
                "full_solution": feedback_data.get("full_solution", ""),
                "mistake_analysis": feedback_data.get("mistake_analysis", ""),
                "improvement_advice": feedback_data.get("improvement_advice", ""),
                "attempt_id": attempt_id,
                "status": "success"
            }
        
        except json.JSONDecodeError as e:
            logger.error(f"JSON decode error: {str(e)}")
            return {
                "full_solution": f"Lỗi xử lý phản hồi AI: {str(e)}",
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
        correct_answer: str,
        insight_type: str = "assessment"
    ) -> Dict[str, Any]:
        """
        Generate educational insights (assessment or roadmap) based on student data.
        """
        try:
            if insight_type == "roadmap":
                prompt_template = ai_roadmap_prompt
            else:
                prompt_template = ai_assessment_prompt
            
            formatted_prompt = prompt_template.format(
                question_text=question_text,
                student_answer=student_answer,
                correct_answer=correct_answer
            )
            
            formatted_prompt += """\n\nCung cấp phản hồi dưới dạng JSON với cấu trúc:
{
    "concepts_to_review": ["Khái niệm 1", "Khái niệm 2"],
    "recommended_exercises": ["Bài tập 1", "Bài tập 2"],
    "quick_tips": ["Mẹo 1", "Mẹo 2"],
    "summary": "Tóm tắt ngắn gọn nhận xét cho em..."
}"""
            
            messages = self._prepare_messages_with_image(formatted_prompt, None)
            
            response = self._call_api_with_retry(messages)
            
            if response.get("Status") == "error":
                return {
                    "status": "error",
                    "error": response.get("Error", "Unknown error")
                }
            
            response_text = response.get("text", "")
            insight_data = json.loads(response_text)
            
            return {
                "concepts_to_review": insight_data.get("concepts_to_review", []),
                "recommended_exercises": insight_data.get("recommended_exercises", []),
                "quick_tips": insight_data.get("quick_tips", []),
                "summary": insight_data.get("summary", ""),
                "status": "success"
            }
            
        except Exception as e:
            logger.error(f"Error generating insight ({insight_type}): {str(e)}")
            return {
                "status": "error",
                "error": str(e),
                "summary": f"AI đang gặp khó khăn khi tạo {insight_type}. Vui lòng thử lại sau giây lát."
            }
    
    # ==================== HELPER METHODS ====================
    def _download_and_encode_image(self, image_url: str) -> Optional[str]:
        """Download image from URL and encode to base64"""
        try:
            if not image_url or not image_url.startswith(('http://', 'https://')):
                return None
            
            response = requests.get(image_url, timeout=10)
            response.raise_for_status()
            
            # Encode to base64
            image_data = base64.b64encode(response.content).decode('utf-8')
            logger.info(f"Downloaded and encoded image from {image_url}")
            
            return image_data
        
        except Exception as e:
            logger.warning(f"Failed to download image from {image_url}: {str(e)}")
            return None
    
    def _prepare_messages_with_image(
        self,
        prompt_text: str,
        image_url: Optional[str] = None
    ) -> List[Dict]:
        """Prepare messages for OpenAI with optional image (vision)"""
        
        content = []
        
        # Add text content
        content.append({
            "type": "text",
            "text": prompt_text
        })
        
        # Add image content if provided
        if image_url:
            image_data = self._download_and_encode_image(image_url)
            if image_data:
                # Determine image media type
                media_type = "image/jpeg"
                if image_url.lower().endswith('.png'):
                    media_type = "image/png"
                elif image_url.lower().endswith('.gif'):
                    media_type = "image/gif"
                elif image_url.lower().endswith('.webp'):
                    media_type = "image/webp"
                
                content.append({
                    "type": "image_url",
                    "image_url": {
                        "url": f"data:{media_type};base64,{image_data}"
                    }
                })
        
        return [
            {
                "role": "user",
                "content": content
            }
        ]
    
    def _call_api_with_retry(self, messages: List[Dict]) -> Dict:
        """Call OpenAI API with retry logic on key rotation"""
        max_retries = len(api_key_manager.api_keys)
        # Import errors for v1.0+
        from openai import RateLimitError, APIError
        
        for attempt in range(max_retries):
            try:
                # Set global key for the default client
                openai.api_key = api_key_manager.get_current_key()
                
                # Using the global completions interface
                response = openai.chat.completions.create(
                    model=self.model_name,
                    messages=messages,
                    temperature=0.7,
                    max_tokens=2000,
                    response_format={"type": "json_object"}
                )
                
                response_text = response.choices[0].message.content
                
                return {
                    "text": response_text,
                    "Status": "success"
                }
            
            except RateLimitError as e:
                logger.error(f"Rate limit hit on attempt {attempt + 1}: {str(e)}")
                
                if attempt < max_retries - 1:
                    # Rotate API key on rate limit
                    api_key_manager.rotate_key()
                else:
                    return {
                        "text": f"API Rate Limit after {max_retries} attempts: {str(e)}",
                        "Status": "error",
                        "Error": str(e)
                    }
            
            except APIError as e:
                logger.error(f"API error on attempt {attempt + 1}: {str(e)}")
                
                if attempt < max_retries - 1:
                    api_key_manager.rotate_key()
                else:
                    return {
                        "text": f"API Error after {max_retries} attempts: {str(e)}",
                        "Status": "error",
                        "Error": str(e)
                    }
            
            except Exception as e:
                logger.error(f"Unexpected error on attempt {attempt + 1}: {str(e)}")
                
                if attempt < max_retries - 1:
                    api_key_manager.rotate_key()
                else:
                    return {
                        "text": f"Error after {max_retries} attempts: {str(e)}",
                        "Status": "error",
                        "Error": str(e)
                    }
        
        return {
            "text": "Unknown error",
            "Status": "error",
            "Error": "Max retries exceeded"
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
) -> Dict[str, Any]:
    """
    Standalone function to get AI hint using OpenAI.
    
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
    service = OpenAIService()
    
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
) -> Dict[str, Any]:
    """
    Standalone function to get AI feedback using OpenAI.
    
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
    service = OpenAIService()
    
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
    service = OpenAIService()
    
    # Test hint
    print("Testing hint generation with OpenAI...")
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
    print("Testing feedback generation with OpenAI...")
    feedback_result = service.generate_feedback(
        question_text="Tính đạo hàm của f(x) = 2x³ + 3x²",
        question_type="FillBlank",
        student_answer="f'(x) = 6x² + 6x",
        correct_answer="f'(x) = 6x² + 6x",
        is_correct=True,
        explanation="Sử dụng quy tắc lũy thừa để tính đạo hàm"
    )
    print(f"Feedback: {json.dumps(feedback_result, ensure_ascii=False, indent=2)}")
