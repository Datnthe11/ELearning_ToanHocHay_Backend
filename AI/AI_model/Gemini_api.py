from typing import Dict, Any, List, Optional
import google.generativeai as genai
import json
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

from Prompts import hint_prompt, feedback_prompt, ai_assessment_prompt, ai_roadmap_prompt

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
    ) -> Dict[str, Any]:
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
                return {
                    "hint_text": "AI đang gặp sự cố khi tạo gợi ý: " + response.get("Error", "Unknown error"),
                    "hint_level": hint_level,
                    "question_id": question_id,
                    "status": "error"
                }
            
            # Parse JSON response
            response_text = response.get("text", "")
            logger.info(f"Raw AI Hint Response: {response_text}")
            
            cleaned_text = self._clean_json_text(response_text)
            hint_data = json.loads(cleaned_text, strict=False)
            
            return {
                "hint_text": hint_data.get("hint_text", response_text),
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "success"
            }
        
        except Exception as e:
            logger.error(f"Error generating hint: {str(e)}")
            return {
                "hint_text": "Lỗi hệ thống khi tạo gợi ý.",
                "hint_level": hint_level,
                "question_id": question_id,
                "status": "error",
                "error": str(e)
            }
    
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
    ) -> Dict[str, Any]:
        """
        Generate comprehensive feedback after exercise completion.
        """
        try:
            # Format options text (including correct answers for feedback)
            options_text = self._format_options(options, include_correct=True)
            
            # Format the prompt
            formatted_prompt = feedback_prompt.format(
                question_text=question_text,
                question_type=question_type,
                correct_answer=correct_answer,
                explanation=explanation or "N/A",
                student_answer=student_answer,
                is_correct=" Đúng" if is_correct else " Sai",
                options_text=options_text
            )
            
            # Add JSON format instruction
            formatted_prompt += "\n\nCung cấp phản hồi JSON: {\"full_solution\": \"...\", \"mistake_analysis\": \"...\", \"improvement_advice\": \"...\"}"
            
            # Prepare content with image if provided
            content = self._prepare_content_with_image(formatted_prompt, question_image_url)
            
            # Call Gemini API with retry logic
            response = self._call_api_with_retry(content)
            
            if response.get("Status") == "error":
                return {"status": "error", "error": response.get("Error", "Unknown")}
            
            # Parse JSON response
            response_text = response.get("text", "")
            logger.info(f"Raw AI Feedback Response: {response_text}")
            
            # Nếu response rỗng → thử lại với key tiếp theo
            if not response_text or not response_text.strip():
                logger.warning("Empty response from Gemini API, rotating key and retrying...")
                api_key_manager.rotate_key()
                api_key_manager.configure()
                self.model = genai.GenerativeModel(
                    self.model_name,
                    generation_config={"response_mime_type": "application/json"}
                )
                retry_response = self._call_api_with_retry(content)
                response_text = retry_response.get("text", "")
                if not response_text or not response_text.strip():
                    logger.error("Empty response even after retry")
                    return {"status": "error", "error": "Empty response from AI"}
            
            cleaned_text = self._clean_json_text(response_text)
            data = json.loads(cleaned_text, strict=False)
            
            return {
                "full_solution": data.get("full_solution", ""),
                "mistake_analysis": data.get("mistake_analysis", ""),
                "improvement_advice": data.get("improvement_advice", ""),
                "attempt_id": attempt_id,
                "status": "success"
            }
        
        except Exception as e:
            logger.error(f"Error in feedback: {str(e)}")
            return {"status": "error", "error": str(e)}

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
            # Select prompt based on type
            if insight_type == "roadmap":
                prompt_template = ai_roadmap_prompt
            else:
                prompt_template = ai_assessment_prompt
            
            # Format the prompt
            formatted_prompt = prompt_template.format(
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
            logger.info(f"Raw AI Insight Response ({insight_type}): {response_text}")
            
            cleaned_text = self._clean_json_text(response_text)
            insight_data = json.loads(cleaned_text, strict=False)
            
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

    def _clean_json_text(self, text: str) -> str:
        """Clean JSON text from Markdown formatting"""
        if not text:
            return ""
        
        text = text.strip()
        # Remove markdown code blocks if present
        if "```json" in text:
            text = text.split("```json")[-1].split("```")[0]
        elif "```" in text:
            text = text.split("```")[-1].split("```")[0]
                
        text = text.strip()
        
        # Đảm bảo import re ở đầu file nếu dùng.
        import re
        # Sửa lỗi Invalid escape do Gemini thỉnh thoảng trả về '\' thừa (vd: '\Tuy nhiên')
        # Dùng regex để thay thế '\' bằng '\\' đối với những escape không hợp lệ của JSON.
        # Bổ sung chữ 'u' cho mã unicode (\uXXXX)
        text = re.sub(r'\\([^"\\/bfnrtu])', r'\\\\\1', text)
        
        return text
    
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
        
        for attempt in range(max_retries):
            try:
                response = self.model.generate_content(content)
                return {
                    "text": response.text.strip(),
                    "Status": "success"
                }
            
            except Exception as e:
                last_error = str(e)
                logger.error(f"API call attempt {attempt + 1} failed: {last_error}")
                
                if attempt < max_retries - 1:
                    # Rotate API key and reconfigure
                    api_key_manager.rotate_key()
                    api_key_manager.configure()
                    # Recreate model with new API key
                    self.model = genai.GenerativeModel(
                        self.model_name,
                        generation_config=genai.types.GenerationConfig(
                            response_mime_type="application/json"
                        )
                    )
                else:
                    return {
                        "Status": "error",
                        "Error": last_error
                    }
        
        return {
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
        
        lines = []
        for idx, opt in enumerate(options, 1):
            line = f"{idx}. {opt.get('OptionText', '')}"
            if include_correct and opt.get('IsCorrect'):
                line += " ✓"
            if opt.get('ImageUrl'):
                line += f" [Hình ảnh: {opt['ImageUrl']}]"
            lines.append(line)
        
        return "\n".join(lines)


# ==================== STANDALONE FUNCTIONS ====================
async def get_ai_hint(
    question_data: Dict,
    student_answer: str,
    hint_level: int = 1
) -> Dict[str, Any]:
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
) -> Dict[str, Any]:
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
    # Simple test
    service = GeminiAIService()
    print("Testing insight generation...")
    res = service.generate_insight("Tính 1+1", "2", "2", "assessment")
    print(json.dumps(res, indent=2, ensure_ascii=False))
