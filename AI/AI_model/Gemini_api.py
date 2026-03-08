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
    
    def generate_hint(self, question_text, question_type, difficulty_level, student_answer, hint_level=1, options=None, question_id=None, question_image_url=None):
        try:
            options_text = self._format_options(options, include_correct=False)
            formatted_prompt = hint_prompt.format(hint_level=hint_level, question_text=question_text, question_type=question_type, difficulty_level=difficulty_level, student_answer=student_answer, options_text=options_text)
            formatted_prompt += "\n\nCung cấp phản hồi dưới dạng JSON với cấu trúc: {\"hint_text\": \"...\", \"hint_level\": 1}"
            content = self._prepare_content_with_image(formatted_prompt, question_image_url)
            response = self._call_api_with_retry(content)
            if response.get("Status") == "error":
                return {"hint_text": "Lỗi AI: " + response.get("Error", "Unknown"), "status": "error"}
            
            response_text = response.get("text", "")
            cleaned_text = self._clean_json_text(response_text)
            hint_data = json.loads(cleaned_text, strict=False)
            return {"hint_text": hint_data.get("hint_text", response_text), "hint_level": hint_level, "question_id": question_id, "status": "success"}
        except Exception as e:
            logger.error(f"Error in hint: {str(e)}")
            return {"hint_text": "Lỗi hệ thống.", "status": "error"}

    def generate_feedback(self, question_text, question_type, student_answer, correct_answer, is_correct, explanation=None, options=None, attempt_id=None, question_image_url=None):
        try:
            options_text = self._format_options(options, include_correct=True)
            formatted_prompt = feedback_prompt.format(question_text=question_text, question_type=question_type, correct_answer=correct_answer, explanation=explanation or "N/A", student_answer=student_answer, is_correct=" Đúng" if is_correct else " Sai", options_text=options_text)
            formatted_prompt += "\n\nCung cấp phản hồi JSON: {\"full_solution\": \"...\", \"mistake_analysis\": \"...\", \"improvement_advice\": \"...\"}"
            content = self._prepare_content_with_image(formatted_prompt, question_image_url)
            response = self._call_api_with_retry(content)
            if response.get("Status") == "error": return {"status": "error"}
            
            response_text = response.get("text", "")
            cleaned_text = self._clean_json_text(response_text)
            data = json.loads(cleaned_text, strict=False)
            return {"full_solution": data.get("full_solution", ""), "mistake_analysis": data.get("mistake_analysis", ""), "improvement_advice": data.get("improvement_advice", ""), "attempt_id": attempt_id, "status": "success"}
        except Exception as e:
            logger.error(f"Error in feedback: {str(e)}")
            return {"status": "error"}

    def generate_insight(self, question_text: str, student_answer: str, correct_answer: str, insight_type: str = "assessment") -> Dict[str, Any]:
        try:
            # Chọn prompt dựa trên type
            if insight_type == "roadmap":
                from Prompts import ai_roadmap_prompt
                prompt_template = ai_roadmap_prompt
            else:
                from Prompts import ai_assessment_prompt
                prompt_template = ai_assessment_prompt
                
            formatted_prompt = prompt_template.format(
                question_text=question_text,
                student_answer=student_answer,
                correct_answer=correct_answer
            )
            
            response = self._call_api_with_retry([formatted_prompt])
            if response.get("Status") == "error":
                return {"status": "error", "error": response.get("Error", "Unknown error")}
            
            response_text = response.get("text", "")
            cleaned_text = self._clean_json_text(response_text)
            
            try:
                insight_data = json.loads(cleaned_text, strict=False)
                return {
                    "concepts_to_review": insight_data.get("concepts_to_review", []),
                    "recommended_exercises": insight_data.get("recommended_exercises", []),
                    "quick_tips": insight_data.get("quick_tips", []),
                    "summary": insight_data.get("summary", ""),
                    "status": "success"
                }
            except json.JSONDecodeError as je:
                logger.error(f"JSON decode error in insight ({insight_type}): {str(je)} - Raw: {response_text}")
                return {
                    "status": "error",
                    "error": str(je),
                    "summary": f"AI đang gặp khó khăn khi tạo {insight_type}. Vui lòng thử lại sau giây lát."
                }
        except Exception as e:
            logger.error(f"Error generating insight ({insight_type}): {str(e)}")
            return {"status": "error", "error": str(e)}

    def _clean_json_text(self, text: str) -> str:
        if not text: return ""
        text = text.strip()
        if "```json" in text:
            text = text.split("```json")[-1].split("```")[0]
        elif "```" in text:
            text = text.split("```")[-1].split("```")[0]
        return text.strip()

    def _download_image(self, image_url: str) -> Optional[Image.Image]:
        try:
            if not image_url or not image_url.startswith(('http://', 'https://')): return None
            response = requests.get(image_url, timeout=10)
            response.raise_for_status()
            return Image.open(BytesIO(response.content))
        except Exception: return None

    def _prepare_content_with_image(self, prompt_text: str, image_url: Optional[str] = None) -> List:
        content = [prompt_text]
        if image_url:
            image = self._download_image(image_url)
            if image: content.append(image)
        return content

    def _call_api_with_retry(self, content) -> Dict:
        max_retries = len(api_key_manager.api_keys)
        last_error = None
        for attempt in range(max_retries):
            try:
                response = self.model.generate_content(content)
                return {"text": response.text.strip(), "Status": "success"}
            except Exception as e:
                last_error = str(e)
                if attempt < max_retries - 1:
                    api_key_manager.rotate_key()
                    api_key_manager.configure()
                    self.model = genai.GenerativeModel(self.model_name, generation_config={"response_mime_type": "application/json"})
                else:
                    return {"Status": "error", "Error": last_error}
        return {"Status": "error", "Error": "Unknown"}

    def _format_options(self, options, include_correct=False) -> str:
        if not options: return "Không có lựa chọn"
        lines = []
        for idx, opt in enumerate(options, 1):
            line = f"{idx}. {opt.get('OptionText', '')}"
            if include_correct and opt.get('IsCorrect'): line += " ✓"
            lines.append(line)
        return "\n".join(lines)
